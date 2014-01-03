﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using System.Windows.Threading;
using System.Threading.Tasks;

namespace FoscamExplorer.Pages
{
    public partial class WifiSettingsPage : PhoneApplicationPage
    {
        DispatcherTimer wifiScanTimer;
        FoscamDevice device;
        PropertyBag deviceParams;

        public WifiSettingsPage()
        {
            InitializeComponent();

            this.Loaded += WifiSettingsPage_Loaded;

            MergeWifiItem(new WifiNetworkInfo() { SSID = "disabled", Security = WifiSecurity.None });
            PasswordBoxWifi.IsEnabled = false;
            
            ErrorMessage.Text = "";
        }

        public FoscamDevice FoscamDevice
        {
            get { return device; }
            set { device = value; }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            StopWifiScanner();
        }

        async void WifiSettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            WifiNetworkList.Focus();

            deviceParams = await device.GetParams();

            ShowParameters(deviceParams);

            CheckBoxAll.Visibility = (DataStore.Instance.Cameras.Count < 2) ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ShowParameters(PropertyBag props)
        {
            PasswordBoxWifi.Password = device.CameraInfo.WifiPassword;

            var network = device.CameraInfo.WifiNetwork;
            if (network != null)
            {
                WifiNetworkList.SelectedIndex = MergeWifiItem(network);
            }

            StartWifiScan();
        }

        private void StartWifiScan()
        {
            device.StartScanWifi();
            wifiScanTimer = new DispatcherTimer();
            wifiScanTimer.Interval = TimeSpan.FromSeconds(2);
            wifiScanTimer.Tick += OnWifiScanTick;
            wifiScanTimer.Start();
        }


        private void StopWifiScanner()
        {
            if (wifiScanTimer != null)
            {
                wifiScanTimer.Tick -= OnWifiScanTick;
                wifiScanTimer.Stop();
                wifiScanTimer = null;
            }
        }

        async void OnWifiScanTick(object sender, object e)
        {
            var found = await device.GetWifiScan();
            if (found != null)
            {
                foreach (var network in found)
                {
                    MergeWifiItem(network);
                }
            }
        }

        int MergeWifiItem(WifiNetworkInfo info)
        {
            int i = 0;
            foreach (WifiNetworkInfo item in WifiNetworkList.Items)
            {
                if (item.SSID == info.SSID)
                {
                    item.Security = info.Security;
                    item.Mode = info.Mode;
                    item.BSSID = info.BSSID;
                    return i;
                }
                i++;
            }

            WifiNetworkList.Items.Add(info);

            return WifiNetworkList.Items.Count - 1;
        }

        private void OnWifiSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var network = WifiNetworkList.SelectedItem as WifiNetworkInfo;
            PasswordBoxWifi.IsEnabled = (network.SSID != "disabled");
            ShowError("");
        }

        void ShowError(string text)
        {
            var quiet = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, new DispatchedHandler(() =>
            {
                ErrorMessage.Text = text;
            }));
        }

        private void OnPasswordGotFocus(object sender, RoutedEventArgs e)
        {
            PasswordBoxWifi.SelectAll();
        }

        private void OnWifiPasswordChanged(object sender, RoutedEventArgs e)
        {

        }

        private async void OnUpdateButtonClick(object sender, RoutedEventArgs e)
        {
            ErrorMessage.Text = "updating...";

            WifiNetworkInfo info = WifiNetworkList.SelectedItem as WifiNetworkInfo;
            if (info != null && info.SSID == "disabled")
            {
                info = null;
            }

            device.CameraInfo.WifiNetwork = info;

            string error = await device.UpdateWifiSettings();
            if (error != null)
            {
                ShowError(error);
                return;
            }

            if (CheckBoxAll.IsChecked == true)
            {
                await UpdateWifiOnAllCameras(info);
            }

            this.NavigationService.GoBack();
        }

        private async Task UpdateWifiOnAllCameras(WifiNetworkInfo info)
        {
            foreach (var camera in DataStore.Instance.Cameras)
            {
                if (camera != this.device.CameraInfo)
                {
                    FoscamDevice temp = new FoscamDevice() { CameraInfo = camera };
                    if (temp.CameraInfo.WifiNetwork == null || temp.CameraInfo.WifiNetwork.SSID != info.SSID || camera.WifiPassword != device.CameraInfo.WifiPassword)
                    {
                        camera.WifiPassword = device.CameraInfo.WifiPassword;
                        temp.CameraInfo.WifiNetwork = info;
                        await device.UpdateWifiSettings();
                    }
                }
            }
        }
    }
}