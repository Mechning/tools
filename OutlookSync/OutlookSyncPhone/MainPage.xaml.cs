﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using OutlookSyncPhone.Resources;
using System.Diagnostics;
using Microsoft.Networking;
using System.Text;
using OutlookSync;
using System.Xml;
using System.IO;
using Windows.Phone.PersonalInformation;
using System.Threading.Tasks;

namespace OutlookSyncPhone
{
    public partial class MainPage : PhoneApplicationPage
    {
        ConnectionManager conmgr = new ConnectionManager(Guid.Parse("F657DBF0-AF29-408F-8F4A-B662D7EA4440"), 12777, 12778);
        ServerProxy proxy;
        ContactStore store;
        int contactIndex;

        // Constructor
        public MainPage()
        {
            InitializeComponent();
            conmgr.ServerFound += OnServerFound;
            
        }

        async void OnServerFound(object sender, ServerEventArgs e)
        {
            proxy = e.Server;
            proxy.MessageReceived += OnMessageReceived;

            await proxy.SendMessage(new Message() { Command = "GetContact", Parameters = contactIndex.ToString() });
        }

        async void OnMessageReceived(object sender, MessageEventArgs e)
        {
            Message m = e.Message;
            if (m.Command == "Contact")
            {
                await MergeContact(m.Parameters);

                contactIndex++;
                await proxy.SendMessage(new Message() { Command = "GetContact", Parameters = contactIndex.ToString() });
            }
        }

        private async Task MergeContact(string xml)
        {
        
            // parse the XML into one of our unified store contacts and save it in our ContactStore.
            UnifiedContact contact = UnifiedContact.Parse(xml);

            StoredContact sc = await store.FindContactByRemoteIdAsync(contact.OutlookEntryId);
            if (sc != null)
            {
                // merge
            }
            else
            {
                sc = new StoredContact(store);
            }

            if (!string.IsNullOrEmpty(contact.DisplayName))
            {
                sc.DisplayName = contact.DisplayName;
            }

            var fullName = contact.CompleteName;
            if (fullName != null)
            {
                if (!string.IsNullOrEmpty(fullName.LastName))
                {
                    sc.FamilyName = fullName.LastName;
                }
                if (!string.IsNullOrEmpty(fullName.FirstName))
                {
                    sc.GivenName = fullName.FirstName;
                }
                if (!string.IsNullOrEmpty(fullName.Title))
                {
                    sc.HonorificPrefix = fullName.Title;
                }
                if (!string.IsNullOrEmpty(fullName.Suffix))
                {
                    sc.HonorificSuffix = fullName.Suffix;
                }
            }
            var dictionary = await sc.GetPropertiesAsync();
            if (contact.Birthday != DateTime.MinValue)
            {
                dictionary[KnownContactProperties.Birthdate] = new DateTimeOffset(contact.Birthday);
            }
            if (!string.IsNullOrEmpty(contact.SignificantOthers))
            {
                dictionary[KnownContactProperties.SignificantOther] = contact.SignificantOthers;
            }
            if (!string.IsNullOrEmpty(contact.Children))
            {
                dictionary[KnownContactProperties.Children] = contact.Children;
            }

            if (!string.IsNullOrEmpty(contact.Nickname))
            {
                dictionary[KnownContactProperties.Nickname] = contact.Nickname;
            }

            // addresses
            UpdateAddress(KnownContactProperties.WorkAddress, AddressKind.Work, contact, dictionary);
            UpdateAddress(KnownContactProperties.Address, AddressKind.Home, contact, dictionary);
            UpdateAddress(KnownContactProperties.OtherAddress, AddressKind.Other, contact, dictionary);

            // email addresses
            UpdateEmailAddress(KnownContactProperties.WorkEmail, EmailAddressKind.Work, contact, dictionary);
            UpdateEmailAddress(KnownContactProperties.Email, EmailAddressKind.Personal, contact, dictionary);
            UpdateEmailAddress(KnownContactProperties.OtherEmail, EmailAddressKind.Other, contact, dictionary);

            // phone numbers
            UpdatePhoneNumber(KnownContactProperties.Telephone, PhoneNumberKind.Home, contact, dictionary);
            UpdatePhoneNumber(KnownContactProperties.CompanyTelephone, PhoneNumberKind.Company, contact, dictionary);
            UpdatePhoneNumber(KnownContactProperties.HomeFax, PhoneNumberKind.HomeFax, contact, dictionary);
            UpdatePhoneNumber(KnownContactProperties.MobileTelephone, PhoneNumberKind.Mobile, contact, dictionary);
            UpdatePhoneNumber(KnownContactProperties.WorkTelephone, PhoneNumberKind.Work, contact, dictionary);
            UpdatePhoneNumber(KnownContactProperties.WorkFax, PhoneNumberKind.WorkFax, contact, dictionary);

            // websites
            if (contact.Websites != null && contact.Websites.Count > 0)
            {
                dictionary[KnownContactProperties.Url] = string.Join(";", contact.Websites);
            }

            sc.RemoteId = contact.OutlookEntryId;
            await sc.SaveAsync();
        }

        private void UpdateAddress(string key, AddressKind kind, UnifiedContact contact, IDictionary<string,object> dictionary)
        {
            if (contact.Addresses != null)
            {
                var address = (from a in contact.Addresses where a.Kind == kind select a).FirstOrDefault();
                if (address != null && address.PhysicalAddress != null)
                {
                    var pa = address.PhysicalAddress;

                    Windows.Phone.PersonalInformation.ContactAddress ca = new Windows.Phone.PersonalInformation.ContactAddress();
                    if (!string.IsNullOrEmpty(pa.CountryRegion))
                    {
                        ca.Country = pa.CountryRegion;
                    }
                    if (!string.IsNullOrEmpty(pa.PostalCode))
                    {
                        ca.PostalCode = pa.PostalCode;
                    }
                    if (!string.IsNullOrEmpty(pa.AddressLine1))
                    {
                        ca.StreetAddress = pa.AddressLine1;
                    }
                    if (!string.IsNullOrEmpty(pa.AddressLine2))
                    {
                        if (!string.IsNullOrEmpty(ca.StreetAddress))
                        {
                            ca.StreetAddress += ", ";
                        }
                        ca.StreetAddress += pa.AddressLine2;
                    }

                    if (!string.IsNullOrEmpty(pa.StateProvince))
                    {
                        ca.Region = pa.StateProvince;
                    }

                    if (!string.IsNullOrEmpty(pa.City))
                    {
                        ca.Locality = pa.City;
                    }

                    dictionary[key] = ca;
                }
            }
        }

        private void UpdateEmailAddress(string key, EmailAddressKind kind, UnifiedContact contact, IDictionary<string, object> dictionary)
        {
            if (contact.EmailAddresses != null)
            {
                ContactEmailAddress email = (from a in contact.EmailAddresses where a.Kind == kind select a).FirstOrDefault();
                if (email != null && !string.IsNullOrEmpty(email.EmailAddress))
                {
                    dictionary[key] = email.EmailAddress;
                }
            }
        }

        private void UpdatePhoneNumber(string key, PhoneNumberKind kind, UnifiedContact contact, IDictionary<string, object> dictionary)
        {
            if (contact.PhoneNumbers != null)
            {
                ContactPhoneNumber phone = (from a in contact.PhoneNumbers where a.Kind == kind select a).FirstOrDefault();
                if (phone != null && !string.IsNullOrEmpty(phone.PhoneNumber))
                {
                    dictionary[key] = phone.PhoneNumber;
                }
            }
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            store = await ContactStore.CreateOrOpenAsync(ContactStoreSystemAccessMode.ReadWrite,
                ContactStoreApplicationAccessMode.ReadOnly);
                       
            conmgr.Start();

            base.OnNavigatedTo(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            conmgr.Stop();
            base.OnNavigatedFrom(e);
        }

        // Sample code for building a localized ApplicationBar
        //private void BuildLocalizedApplicationBar()
        //{
        //    // Set the page's ApplicationBar to a new instance of ApplicationBar.
        //    ApplicationBar = new ApplicationBar();

        //    // Create a new button and set the text value to the localized string from AppResources.
        //    ApplicationBarIconButton appBarButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/appbar.add.rest.png", UriKind.Relative));
        //    appBarButton.Text = AppResources.AppBarButtonText;
        //    ApplicationBar.Buttons.Add(appBarButton);

        //    // Create a new menu item with the localized string from AppResources.
        //    ApplicationBarMenuItem appBarMenuItem = new ApplicationBarMenuItem(AppResources.AppBarMenuItemText);
        //    ApplicationBar.MenuItems.Add(appBarMenuItem);
        //}
    }
}