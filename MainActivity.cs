using System;
using Android.App;
using Android.OS;
using Android.Runtime;
using Android.Views;
using AndroidX.AppCompat.Widget;
using AndroidX.AppCompat.App;
using Google.Android.Material.FloatingActionButton;
using Google.Android.Material.Snackbar;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Util;
using Hoho.Android.UsbSerial.Driver;
using Hoho.Android.UsbSerial.Extensions;
using Hoho.Android.UsbSerial.Util;
using Hoho.Android.UsbSerial.Driver;
using Hoho.Android.UsbSerial.Extensions;
using Hoho.Android.UsbSerial.Util;
using UsbSerialExampleApp;

namespace _09_06_2022
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
    [IntentFilter(new[] { UsbManager.ActionUsbDeviceAttached })]
    [MetaData(UsbManager.ActionUsbDeviceAttached, Resource = "@xml/device_filter")]
    public class MainActivity : Activity
    {
        static readonly string TAG = typeof(MainActivity).Name;
        const string ACTION_USB_PERMISSION = "com.hoho.android.usbserial.examples.USB_PERMISSION";

        UsbManager usbManager;
        ListView listView;
        TextView progressBarTitle;
        ProgressBar progressBar;

        UsbSerialPortAdapter adapter;
        BroadcastReceiver detachedReceiver;
        UsbSerialPort selectedPort;


        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.content_main);

            usbManager = GetSystemService(Context.UsbService) as UsbManager;
            listView = FindViewById<ListView>(Resource.Id.deviceList);
            progressBar = FindViewById<ProgressBar>(Resource.Id.progressBar);
            progressBarTitle = FindViewById<TextView>(Resource.Id.progressBarTitle);
        }

        protected override async void OnResume()
        {
            base.OnResume();

            adapter = new UsbSerialPortAdapter(this);
            listView.Adapter = adapter;

            listView.ItemClick += async (sender, e) => {
                await OnItemClick(sender, e);
            };

            await PopulateListAsync();

            //register the broadcast receivers
            detachedReceiver = new UsbDeviceDetachedReceiver(this);
            RegisterReceiver(detachedReceiver, new IntentFilter(UsbManager.ActionUsbDeviceDetached));
        }
        protected override void OnPause()
        {
            base.OnPause();

            // unregister the broadcast receivers
            var temp = detachedReceiver; // copy reference for thread safety
            if (temp != null)
                UnregisterReceiver(temp);
        }
        internal static Task<IList<IUsbSerialDriver>> FindAllDriversAsync(UsbManager usbManager)
        {
            // using the default probe table
            // return UsbSerialProber.DefaultProber.FindAllDriversAsync (usbManager);

            // adding a custom driver to the default probe table
            var table = UsbSerialProber.DefaultProbeTable;
            //table.AddProduct(0x1b4f, 0x0008, typeof(CdcAcmSerialDriver)); // IOIO OTG

            //table.AddProduct(0x09D8, 0x0420, typeof(CdcAcmSerialDriver)); // Elatec TWN4
            table.AddProduct(0x1a86, 0x7523, typeof(Ch34xSerialDriver));

            table.AddProduct(0x0483, 0x5740, typeof(STM32SerialDriver)); //typeof(STM32SerialDriver))// Kurokesu   <!-- 0x0483 / 0x5740: Kurokesu SCF-4 M -->

            var prober = new UsbSerialProber(table);
            return prober.FindAllDriversAsync(usbManager);
        }

        async Task OnItemClick(object sender, AdapterView.ItemClickEventArgs e)
        {
            Log.Info(TAG, "Pressed item " + e.Position);
            if (e.Position >= adapter.Count)
            {
                Log.Info(TAG, "Illegal position.");
                return;
            }

            // request user permission to connect to device
            // NOTE: no request is shown to user if permission already granted
            selectedPort = adapter.GetItem(e.Position);
            var permissionGranted = await usbManager.RequestPermissionAsync(selectedPort.Driver.Device, this);
            if (permissionGranted)
            {
                // start the SerialConsoleActivity for this device
                var newIntent = new Intent(this, typeof(SerialConsoleActivity));
                newIntent.PutExtra(SerialConsoleActivity.EXTRA_TAG, new UsbSerialPortInfo(selectedPort));
                StartActivity(newIntent);
            }
        }

        async Task PopulateListAsync()
        {
            ShowProgressBar();

            Log.Info(TAG, "Refreshing device list ...");

            var drivers = await FindAllDriversAsync(usbManager);

            adapter.Clear();
            foreach (var driver in drivers)
            {
                var ports = driver.Ports;
                Log.Info(TAG, string.Format("+ {0}: {1} port{2}", driver, ports.Count, ports.Count == 1 ? string.Empty : "s"));
                foreach (var port in ports)
                    adapter.Add(port);
            }

            adapter.NotifyDataSetChanged();
            progressBarTitle.Text = string.Format("{0} device{1} found", adapter.Count, adapter.Count == 1 ? string.Empty : "s");
            HideProgressBar();
            Log.Info(TAG, "Done refreshing, " + adapter.Count + " entries found.");
        }

        void ShowProgressBar()
        {
            progressBar.Visibility = ViewStates.Visible;
            progressBarTitle.Text = GetString(Resource.String.refreshing);
        }

        void HideProgressBar()
        {
            progressBar.Visibility = ViewStates.Invisible;
        }


        #region UsbSerialPortAdapter implementation

        class UsbSerialPortAdapter : ArrayAdapter<UsbSerialPort>
        {
            public UsbSerialPortAdapter(Context context)
                : base(context, global::Android.Resource.Layout.SimpleExpandableListItem2)
            {
            }

            public override View GetView(int position, View convertView, ViewGroup parent)
            {
                var row = convertView;
                if (row == null)
                {
                    var inflater = Context.GetSystemService(Context.LayoutInflaterService) as LayoutInflater;
                    row = inflater.Inflate(global::Android.Resource.Layout.SimpleListItem2, null);
                }

                var port = this.GetItem(position);
                var driver = port.GetDriver();
                var device = driver.GetDevice();

                var title = string.Format("Vendor {0} Product {1}",
                    HexDump.ToHexString((short)device.VendorId),
                    HexDump.ToHexString((short)device.ProductId));
                row.FindViewById<TextView>(global::Android.Resource.Id.Text1).Text = title;

                var subtitle = device.Class.SimpleName;
                row.FindViewById<TextView>(global::Android.Resource.Id.Text2).Text = subtitle;

                return row;
            }
        }

        #endregion

        #region UsbDeviceDetachedReceiver implementation

        class UsbDeviceDetachedReceiver
            : BroadcastReceiver
        {
            readonly string TAG = typeof(UsbDeviceDetachedReceiver).Name;
            readonly MainActivity activity;

            public UsbDeviceDetachedReceiver(MainActivity activity)
            {
                this.activity = activity;
            }

            public async override void OnReceive(Context context, Intent intent)
            {
                var device = intent.GetParcelableExtra(UsbManager.ExtraDevice) as UsbDevice;

                Log.Info(TAG, "USB device detached: " + device.DeviceName);

                await activity.PopulateListAsync();
            }
        }

        #endregion


    }

}
