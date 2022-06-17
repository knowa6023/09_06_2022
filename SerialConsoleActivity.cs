/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Hardware.Usb;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;

using Hoho.Android.UsbSerial.Driver;
using Hoho.Android.UsbSerial.Extensions;
using Hoho.Android.UsbSerial.Util;
using _09_06_2022;


namespace UsbSerialExampleApp
{
    [Activity(Label = "@string/app_name", LaunchMode = LaunchMode.SingleTop)]
    public class SerialConsoleActivity : Activity
    {
        static readonly string TAG = typeof(SerialConsoleActivity).Name;

        public const string EXTRA_TAG = "PortInfo";
        const int READ_WAIT_MILLIS = 200;
        const int WRITE_WAIT_MILLIS = 200;

        UsbSerialPort port;

        UsbManager usbManager;
        TextView titleTextView;
        TextView dumpTextView;
        ScrollView scrollView;
        Button sleepButton;
        Button wakeButton;
        Button ledOnButton;
        Button ledOffButton;

        SerialInputOutputManager serialIoManager;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            Log.Info(TAG, "OnCreate");

            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.serial_console);

            usbManager = GetSystemService(Context.UsbService) as UsbManager;
            titleTextView = FindViewById<TextView>(Resource.Id.demoTitle);
            dumpTextView = FindViewById<TextView>(Resource.Id.consoleText);
            scrollView = FindViewById<ScrollView>(Resource.Id.demoScroller);

            sleepButton = FindViewById<Button>(Resource.Id.sleepButton);
            wakeButton = FindViewById<Button>(Resource.Id.wakeupButton);
            ledOffButton = FindViewById<Button>(Resource.Id.ledOffButton);
            ledOnButton = FindViewById<Button>(Resource.Id.ledOnButton);
            // The following arrays contain data that is used for a custom firmware for
            // the Elatec TWN4 RFID reader. This code is included here to show how to
            // send data back to a USB serial device
            byte[] sleepdata = new byte[] { 0x24, 0x53, 0x0d, 0x0A };
            //byte[] tmp_data_char = new byte[] //"!1/r/n";
            byte[] wakedata = new byte[] {0x24, 0x31, 0x0d, 0x0A };//{ 0xf0, 0x04, 0x11, 0xf1 };
            byte[] ledData_1 = new byte[] { 0x4D, 0x37, 0x0d, 0x0A };
            byte[] ledData_2 = new byte[] { 0x4D, 0x38, 0x0d, 0x0A };
            byte[] ledOff_bytes = new byte[] { 0x21, 0x31, 0x0d, 0x0A };
            byte[] ledOn_bytes = new byte[] { 0x21, 0x31 };//{ 0x4D, 0x37, 0x0d, 0x0A };
            sleepButton.Click += delegate
            {
                WriteData(sleepdata);
            };

            wakeButton.Click += delegate
            {
                WriteData(wakedata);
                //byte[] buf = new byte[100];
                //ReadData(buf);
                //titleTextView.Text = Convert.ToString(buf);
            };


            ledOnButton.Click += delegate
            {
                WriteData(ledData_1);
                //byte[] buf = new byte[100];
                //ReadData(buf);
                //titleTextView.Text = Convert.ToString(buf);

            };

            ledOffButton.Click += delegate
            {
                WriteData(ledData_2);
            };
        }

        protected override void OnPause()
        {
            Log.Info(TAG, "OnPause");

            base.OnPause();

            if (serialIoManager != null && serialIoManager.IsOpen)
            {
                Log.Info(TAG, "Stopping IO manager ..");
                try
                {
                    serialIoManager.Close();
                }
                catch (Java.IO.IOException)
                {
                    // ignore
                }
            }
        }

        protected async override void OnResume()
        {
            Log.Info(TAG, "OnResume");

            base.OnResume();

            var portInfo = Intent.GetParcelableExtra(EXTRA_TAG) as UsbSerialPortInfo;
            int vendorId = portInfo.VendorId;
            int deviceId = portInfo.DeviceId;
            int portNumber = portInfo.PortNumber;

            Log.Info(TAG, string.Format("VendorId: {0} DeviceId: {1} PortNumber: {2}", vendorId, deviceId, portNumber));

            var drivers = await MainActivity.FindAllDriversAsync(usbManager);
            var driver = drivers.Where((d) => d.Device.VendorId == vendorId && d.Device.DeviceId == deviceId).FirstOrDefault();
            if (driver == null)
                throw new Exception("Driver specified in extra tag not found.");

            port = driver.Ports[portNumber];
            if (port == null)
            {
                titleTextView.Text = "No serial device.";
                return;
            }
            Log.Info(TAG, "port=" + port);

            titleTextView.Text = "Serial device: " + port.GetType().Name;

            serialIoManager = new SerialInputOutputManager(port)
            {
                BaudRate = 19200,
                DataBits = 8,
                StopBits = StopBits.One,
                Parity = Parity.None,

            };
            serialIoManager.DataReceived += (sender, e) => {
                RunOnUiThread(() => {
                    //byte k = 0x00;
                    //for (int i = 0; i < e.Data.Length; i++)
                    //{
                    //    e.Data[i] = k;
                    //    k++;
                    //}
                    UpdateReceivedData(e.Data);
                    titleTextView.Text = "DataReceived";
                });
            };
            serialIoManager.ErrorReceived += (sender, e) => {
                RunOnUiThread(() => {
                    var intent = new Intent(this, typeof(MainActivity));
                    titleTextView.Text = "Error";
                    StartActivity(intent);
                });
            };
            titleTextView.Text = "cool? ";
            Log.Info(TAG, "Starting IO manager ..");
            try
            {
                serialIoManager.Open(usbManager);
            }
            catch (Java.IO.IOException e)
            {
                titleTextView.Text = "Error opening device: " + e.Message;
                return;
            }
        }

        void ReadData(byte[] data) 
        {
            port.Read(data, READ_WAIT_MILLIS);     
        }

        void WriteData(byte[] data)
        {
            if (serialIoManager.IsOpen)
            {
                titleTextView.Text = "serialIoManager.IsOpen, write_data";
                port.Write(data, WRITE_WAIT_MILLIS);
            }
        }

        void UpdateReceivedData(byte[] data)
        {
            //byte k = 0x00;
            //for(int i = 0; i < data.Length; i++)
            //{
            //    data[i] = k;
            //    k++;
            //}
            var message = "Read " + data.Length + " bytes: \n"
                + HexDump.DumpHexString(data) + "\n\n";
            //titleTextView.Text = message;
            dumpTextView.Append(message);
            scrollView.SmoothScrollTo(0, dumpTextView.Bottom);
        }
    }
}