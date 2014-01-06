﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using Microsoft.Networking;
using OutlookSync.Model;
using System.Collections.ObjectModel;
using OutlookSync.Utilities;

namespace OutlookSync
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        ConnectionManager conmgr;
        UnifiedStore store;
        public const string StoreFolder = @"LovettSoftware\OutlookSync";
        public const string StoreFileName = "store.xml";
        public const string SettingsFileName = "settings.xml";
        public const string LogFileName = "log.txt";
        Dictionary<string, ConnectedPhone> connected = new Dictionary<string, ConnectedPhone>();
        ObservableCollection<ConnectedPhone> items = new ObservableCollection<ConnectedPhone>();
        Settings settings;

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += OnLoaded;

            Log.OpenLog(GetLogFileName());

            Debug.WriteLine("Starting time " + UnifiedStore.SyncTime);

            PhoneList.ItemsSource = items;
            conmgr = new ConnectionManager("F657DBF0-AF29-408F-8F4A-B662D7EA4440", 12777);
        }

        string GetStoreFileName()
        {
            string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), StoreFolder);
            string file = System.IO.Path.Combine(dir, StoreFileName);
            return file;
        }


        string GetSettingsFileName()
        {
            string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), StoreFolder);
            string file = System.IO.Path.Combine(dir, SettingsFileName);
            return file;
        }

        string GetLogFileName()
        {
            string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), StoreFolder);
            string file = System.IO.Path.Combine(dir, LogFileName);
            return file;
        }

        async void OnLoaded(object sender, RoutedEventArgs e)
        {
            settings = await Settings.LoadAsync(GetSettingsFileName());

            ShowStatus(OutlookSync.Properties.Resources.WaitingForPhone);

            conmgr.StartListening();
            conmgr.MessageReceived += OnMessageReceived;
            conmgr.ReadException += OnServerException;
            store = await UnifiedStore.LoadAsync(GetStoreFileName());
            
            // wait for phone to connect then sync with the phone.
            FirewallConfig fc = new FirewallConfig();
            await fc.CheckSettings();
        }

        private void OnServerException(object sender, ServerExceptionEventArgs e)
        {
            OnDisconnect(e.RemoteEndPoint);
        }

        private void OnDisconnect(System.Net.IPEndPoint endpoint)
        {
            string key = endpoint.Address.ToString();
            ConnectedPhone phone = null;
            connected.TryGetValue(key, out phone);

            if (phone != null)
            {
                phone.Connected = false;

                connected.Remove(key);

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    items.Remove(phone);
                    if (items.Count == 0)
                    {
                        ShowStatus(OutlookSync.Properties.Resources.WaitingForPhone);
                    }
                    else
                    {
                        ShowStatus("");
                    }
                    UpdateConnector();
                }));
            }
        }


        void OnMessageReceived(object sender, MessageEventArgs e)
        {
            Log.WriteLine("Message received from " + e.RemoteEndPoint.ToString());
            Log.WriteLine("  Command: " + e.Message.Command + "(" + e.Message.Parameters + ")");

            e.Response = HandleMessage(e);
        }

        async Task<Message> HandleMessage(MessageEventArgs e)
        {
            Debug.WriteLine(e.Message.Command);
            var msg = e.Message;
            switch (msg.Command)
            {
                case "Hello":
                    await OnCreatePhone(e);
                    break;
                case "Disconnect":
                    OnDisconnect(e.RemoteEndPoint);
                    break;
                case "Connect":
                    OnConnect(e);
                    break;
                case "FinishUpdate":                    
                    await store.SaveAsync(GetStoreFileName());
                    ShowStatus(Properties.Resources.SyncComplete);
                    break;
            }

            string key = e.RemoteEndPoint.Address.ToString();
            ConnectedPhone phone = null;
            connected.TryGetValue(key, out phone);

            Message response = null;
            if (phone != null)
            {
                response = phone.HandleMessage(e);
            }

            return response;
        }

        private void ShowStatus(string msg)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                StatusMessage.Text = msg;
            }));
        }

        bool busy;
        HashSet<string> hosedPhones = new HashSet<string>();

        private async Task OnCreatePhone(MessageEventArgs e)
        {
            if (busy)
            {
                // ignore this hello
                return;
            }

            if (this.store == null)
            {
                // still loading, not done yet!
                return;
            }

            busy = true;
            try
            {
                var msg = e.Message;

                string phoneAppVersion = "";
                string phoneName = "Unknown";
                string phoneId = "";
                string parameters = msg.Parameters;
                if (parameters != null)
                {
                    string[] parts = msg.Parameters.Split('/');
                    if (parts.Length > 0)
                    {
                        phoneAppVersion = parts[0];
                    }
                    if (parts.Length > 1)
                    {
                        phoneName = parts[1];
                    }
                    if (parts.Length > 2)
                    {
                        phoneId = Uri.UnescapeDataString(parts[2]);
                    }
                }

                if (hosedPhones.Contains(phoneId))
                {
                    return;
                }

                string currentAppVersion = Updater.GetCurrentVersion().ToString();
                if (phoneAppVersion != currentAppVersion)
                {
                    // version mismatch!! time to check for updates...
                    var info = await Updater.CheckForUpdate();
                    if (info != null && info.AvailableVersion.ToString() == phoneAppVersion)
                    {
                        await Updater.Update();

                        Dispatcher.Invoke(new Action(() =>
                        {
                            MessageBox.Show(Properties.Resources.UpdateRequired, Properties.Resources.UpdatePrompt, MessageBoxButton.OK);
                            this.Close();
                        }));
                    }
                    else if (info != null)
                    {
                        // update is not available you are hosed!!
                        Dispatcher.Invoke(new Action(() =>
                        {
                            MessageBox.Show(string.Format(Properties.Resources.UpdateMismatch, phoneAppVersion, currentAppVersion), Properties.Resources.UpdatePrompt, MessageBoxButton.OK);
                        }));
                        ShowStatus(Properties.Resources.WaitingForUpdate);
                        hosedPhones.Add(phoneId);
                        return;
                    }
                    else
                    {
                        // update is not available you are hosed!!
                        Dispatcher.Invoke(new Action(() =>
                        {
                            MessageBox.Show(string.Format(Properties.Resources.UpdateMissing, phoneAppVersion), Properties.Resources.UpdatePrompt, MessageBoxButton.OK);
                        }));
                        hosedPhones.Add(phoneId);
                        ShowStatus(Properties.Resources.WaitingForUpdate);
                        return;
                    }
                }

                string key = e.RemoteEndPoint.Address.ToString();

                ConnectedPhone phone = null;
                connected.TryGetValue(key, out phone);

                if (phone == null)
                {
                    phone = new ConnectedPhone(store, this.Dispatcher, GetStoreFileName());
                    phone.IPEndPoint = e.RemoteEndPoint.ToString();
                    phone.PropertyChanged += OnPhonePropertyChanged;
                    connected[key] = phone;

                    await Dispatcher.BeginInvoke(new Action(() =>
                    {
                        phone.Name = phoneName;
                        phone.Id = phoneId;
                        items.Add(phone);
                        UpdateConnector();
                    }));

                    ShowStatus(Properties.Resources.LoadingOutlookContacts);

                    // get new contact info from outlook ready for syncing with this phone.
                    await phone.SyncOutlook();

                    ShowStatus(string.Format(Properties.Resources.LoadedCount, store.Contacts.Count));

                }
                else
                {
                    await Dispatcher.BeginInvoke(new Action(() =>
                    {
                        // phone reconnected, so start over.
                        if (!phone.Connected)
                        {
                            phone.Allowed = false;
                        }
                        if (settings.TrustedPhones.Contains(phone.Id))
                        {
                            phone.Allowed = true;
                        }
                    }));
                }
            }
            finally
            {
                busy = false;
            }
        }

        private void OnPhonePropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            ConnectedPhone phone = (ConnectedPhone)sender;
            if (e.PropertyName == "Allowed" && phone.Allowed)
            {
                if (!string.IsNullOrEmpty(phone.Id))
                {
                    if (!settings.TrustedPhones.Contains(phone.Id))
                    {
                        settings.TrustedPhones.Add(phone.Id);
                    }
                }

                // ok, user has given us the green light to connect this phone.
                conmgr.AllowRemoteMachine(phone.IPEndPoint);
            }
        }

        private void OnConnect(MessageEventArgs e)
        {
            var msg = e.Message;
            string key = e.RemoteEndPoint.Address.ToString();
            ConnectedPhone phone = null;
            connected.TryGetValue(key, out phone);

            if (phone == null)
            {
                // sorry, this phone is not allowed...
                return;
            }

            phone.Connected = true;
        }

        private void UpdateConnector()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Connector.Connected = items.Count > 0;
            }));
        }

        protected async override void OnClosed(EventArgs e)
        {
            conmgr.StopListening();
            await settings.SaveAsync();
            Log.CloseLog();
            base.OnClosed(e);
        }

    }
}
