using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Windows.Devices.Enumeration;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using System.Diagnostics;
using System.Timers;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.Devices.Radios;

namespace SWConnector
{
    class ShearwaterDevice
    {
        private Guid ShearwaterServiceGuid = Guid.Parse("fe25c237-0ece-443c-b0aa-e02033e7029d");
        private Guid ShearwaterCharacteristicGuid = Guid.Parse("27b7570b-359e-45a3-91bb-cf7e70049bd2");

        public ShearwaterDevice(DeviceInformation DevInfo)
        {
            if (DevInfo != null)
            {
                Console.WriteLine(String.Format("Constructed a {0} device", DevInfo.Name));
                _devInfo = DevInfo;
            }
            else
                throw new Exception("Device information is empty");
        }

        ~ShearwaterDevice()
        {
            _shearwaterBLEDevice.Dispose();
        }

        public String getName()
        {
            return _devInfo.Name;
        }
        public String getBTAddress()
        {
            return _devInfo.Id;
        }
        public async Task<bool> Connect()
        {
            _shearwaterBLEDevice = await BluetoothLEDevice.FromIdAsync(_devInfo.Id);
            if (_shearwaterBLEDevice == null)
                throw new Exception("Could not connect to the Shearwater device");

            return true;
        }

        public async Task<bool> EnumerateServices()
        {
            Console.WriteLine("Enumarating supported services");
            GattDeviceServicesResult result = await _shearwaterBLEDevice.GetGattServicesAsync();
            if (result.Status != GattCommunicationStatus.Success)
                throw new Exception("Could not enum for services");

            _devServices = result.Services;
            return true;
        }

        public void GetSWCommunicationService()
        {
            foreach (var service in _devServices)
            {
                if (service.Uuid == ShearwaterServiceGuid)
                {
                    _devCommunicationservice = service;
                    return;
                }
            }

            throw new Exception("Shearwater communication service could not be found");
        }

        public async Task<bool> EnumerateCharacteristics()
        {
            GattCharacteristicsResult result = await _devCommunicationservice.GetCharacteristicsAsync();
            if (result.Status != GattCommunicationStatus.Success)
                throw new Exception("Could not enum for characteristics");

            _devCharacteristics = result.Characteristics;
            return true;
        }

        public void GetSPPCharacteristic()
        {
            foreach (var characteristic in _devCharacteristics)
            {
                if (characteristic.Uuid == ShearwaterCharacteristicGuid)
                {
                    _devSPPCharacteristic = characteristic;
                    GattCharacteristicProperties properties = characteristic.CharacteristicProperties;
                    if (properties.HasFlag(GattCharacteristicProperties.Read))
                    {
                        Console.WriteLine(String.Format("-Reading enabled"));
                    }
                    if (properties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse))
                    {
                        Console.WriteLine(String.Format("-Writing without response enabled"));
                    }
                    if (properties.HasFlag(GattCharacteristicProperties.Notify))
                    {
                        Console.WriteLine(String.Format("-Subscribing enabled"));
                    }
                    return;
                }

                throw new Exception("Shearwater SPP characteristic could not be found");
            }
        }

        public async Task<bool> RestartTimer()
        {
            var cccdValue = GattClientCharacteristicConfigurationDescriptorValue.None;
            if (_devSPPCharacteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate))
            {
                cccdValue = GattClientCharacteristicConfigurationDescriptorValue.Indicate;
            }

            else if (_devSPPCharacteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify))
            {
                cccdValue = GattClientCharacteristicConfigurationDescriptorValue.Notify;
            }

            var status = await _devSPPCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(cccdValue);
            if (status == GattCommunicationStatus.Success)
            {
                //_devSPPCharacteristic.ValueChanged += Characteristic_ValueChanged;
            }

            status = await _devSPPCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None);
            if (status == GattCommunicationStatus.Success)
            {
                //_devSPPCharacteristic.ValueChanged -= Characteristic_ValueChanged;
            }
            status = await _devSPPCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(cccdValue);

            return true;
        }


        private readonly DeviceInformation _devInfo;
        private BluetoothLEDevice _shearwaterBLEDevice;
        private IReadOnlyList<GattDeviceService> _devServices;
        private GattDeviceService _devCommunicationservice;
        private IReadOnlyList<GattCharacteristic> _devCharacteristics;
        private GattCharacteristic _devSPPCharacteristic;
    }

    class BLEConnector
    {
        private DeviceWatcher deviceWatcher = null;
        private readonly List<DeviceInformation> DiscoveredDevices = new List<DeviceInformation>();
        private readonly List<DeviceInformationUpdate> UpdatedDevices = new List<DeviceInformationUpdate>();
        private readonly List<DeviceInformation> NewDevices = new List<DeviceInformation>();

        public BLEConnector()
        {
            string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected", "System.Devices.Aep.Bluetooth.Le.IsConnectable" };
            string aqsAllBluetoothLEDevices = "(System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\")";
            deviceWatcher = DeviceInformation.CreateWatcher(aqsAllBluetoothLEDevices,
                                                            requestedProperties,
                                                            DeviceInformationKind.AssociationEndpoint);

            deviceWatcher.Added += DeviceWatcher_Added;
            deviceWatcher.Updated += DeviceWatcher_Updated;
            deviceWatcher.Removed += DeviceWatcher_Removed;
            deviceWatcher.EnumerationCompleted += DeviceWatcher_EnumerationCompleted;
            deviceWatcher.Stopped += DeviceWatcher_Stopped;
        }

        private void EndOfDiscovery()
        {
            if (DiscoveredDevices.Count > 0)
            {
                Console.WriteLine(String.Format("Found {0} devices", NewDevices.Count));
                ConnectToShearwaterDevice();
            }
            else
            {
                Console.WriteLine("Did not find any device");
            }
        }

        private void RemoveUnwantedDevices()
        {
            foreach (var discoveredDevice in DiscoveredDevices)
            {
                foreach (var updatedDevice in UpdatedDevices)
                {
                    if (discoveredDevice.Id == updatedDevice.Id)
                    {
                        if (!NewDevices.Contains(discoveredDevice))
                            NewDevices.Add(discoveredDevice);
                    }
                }
            }
        }

        private void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation deviceInfo)
        {
            if (sender == deviceWatcher)
                DiscoveredDevices.Add(deviceInfo);
        }

        private void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {
            if (sender == deviceWatcher)
                UpdatedDevices.Add(deviceInfoUpdate);
        }

        private void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate deviceInfoUpdate)
        {
        }

        private void DeviceWatcher_EnumerationCompleted(DeviceWatcher sender, object e)
        {
            if (sender == deviceWatcher)
            {
                RemoveUnwantedDevices();
                EndOfDiscovery();
            }
        }

        private void DeviceWatcher_Stopped(DeviceWatcher sender, object e)
        {
        }

        void StopDeviceWatcher()
        {
            deviceWatcher.Stop();
            deviceWatcher = null;
        }

        public void Start()
        {
            deviceWatcher.Start();
        }

        public void Stop()
        {
            StopDeviceWatcher();
        }

        public async void ConnectToShearwaterDevice()
        {
            foreach (var device in NewDevices)
            {
                if ((device.Name == "Perdix") | (device.Name == "Petrel"))
                {
                    ShearwaterDevice SWDevice = new ShearwaterDevice(device);
                    await SWDevice.Connect();
                    await SWDevice.EnumerateServices();
                    SWDevice.GetSWCommunicationService();
                    await SWDevice.EnumerateCharacteristics();
                    SWDevice.GetSPPCharacteristic();
                    while (true) 
                    {
                        await SWDevice.RestartTimer();
                    }

                }
            }
        }

        class SWConnector
        {
            static BLEConnector Connector = null;

            static async Task<bool> CheckBT()
            {
                var radios = await Radio.GetRadiosAsync();
                foreach(var radio in radios)
                {
                    if ((radio.Kind == RadioKind.Bluetooth) && (radio.State == RadioState.On))
                        return true;
                }

                throw new Exception("Could not find a working BT");
            }

            static void Main()
            {
                try
                {
                    CheckBT().GetAwaiter().GetResult();
                    Console.WriteLine("Shearwater Connector Starting...");
                    Connector = new BLEConnector();
                    Connector.Start();
                    Console.WriteLine("Waiting for Shearwater Computer...");
                    while (true) { }
                }
                catch(System.Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }
    }
}
