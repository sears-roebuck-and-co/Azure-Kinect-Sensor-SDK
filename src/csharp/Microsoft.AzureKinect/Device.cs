﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

[assembly: CLSCompliant(true)]

namespace Microsoft.AzureKinect
{
    public class Device : IDisposable
    {
        private Device(NativeMethods.k4a_device_t handle)
        {
            this.handle = handle;
        }
        public static int GetInstalledCount()
        {
            return (int)NativeMethods.k4a_device_get_installed_count();
        }

        public static Device Open(int index)
        {
            Exception.ThrowIfNotSuccess(NativeMethods.k4a_device_open((uint)index, out NativeMethods.k4a_device_t handle));
            return new Device(handle);
        }

        private string serialNum = null;

        public string SerialNum
        {
            get
            {
                lock (this)
                {
                    if (disposedValue)
                        throw new ObjectDisposedException(nameof(Device));

                    if (serialNum != null)
                    {
                        return serialNum;
                    }
                    else
                    {
                        // Determine the required string size
                        UIntPtr size = new UIntPtr(0);
                        if (NativeMethods.k4a_buffer_result_t.K4A_BUFFER_RESULT_TOO_SMALL != NativeMethods.k4a_device_get_serialnum(handle, null, ref size))
                        {
                            throw new Exception($"Unexpected result calling { nameof(NativeMethods.k4a_device_get_serialnum) }");
                        }

                        // Allocate a string buffer
                        StringBuilder serialno = new StringBuilder((int)size.ToUInt32());

                        // Get the serial number
                        Exception.ThrowIfNotSuccess(NativeMethods.k4a_device_get_serialnum(handle, serialno, ref size));

                        this.serialNum = serialno.ToString();

                        return this.serialNum;
                    }
                }
            }
        }

        public Calibration GetCalibration(DepthMode depthMode, ColorResolution colorResolution)
        {
            Exception.ThrowIfNotSuccess(NativeMethods.k4a_device_get_calibration(handle, depthMode, colorResolution, out Calibration calibration));
            return calibration;
        }

        public byte[] GetRawCalibration()
        {
            lock (this)
            {
                if (disposedValue)
                    throw new ObjectDisposedException(nameof(Device));

                // Determine the required calibration size
                UIntPtr size = new UIntPtr(0);
                if (NativeMethods.k4a_buffer_result_t.K4A_BUFFER_RESULT_TOO_SMALL != NativeMethods.k4a_device_get_raw_calibration(handle, null, ref size))
                {
                    throw new Exception($"Unexpected result calling { nameof(NativeMethods.k4a_device_get_raw_calibration) }");
                }

                // Allocate a string buffer
                byte[] raw = new byte[size.ToUInt32()];

                // Get the raw calibration
                Exception.ThrowIfNotSuccess(NativeMethods.k4a_device_get_raw_calibration(handle, raw, ref size));

                return raw;
            }
        }

        public Capture GetCapture(int timeout_in_ms = -1)
        {
            lock (this)
            {
                if (disposedValue)
                    throw new ObjectDisposedException(nameof(Device));

                NativeMethods.k4a_wait_result_t result = NativeMethods.k4a_device_get_capture(handle, out NativeMethods.k4a_capture_t capture, timeout_in_ms);

                if (result == NativeMethods.k4a_wait_result_t.K4A_WAIT_RESULT_TIMEOUT)
                {
                    throw new TimeoutException("Timed out waiting for capture");
                }

                Exception.ThrowIfNotSuccess(result);

                if (capture.IsInvalid)
                {
                    throw new System.Exception("k4a_device_get_capture did not return a valid capture handle");
                }

                return new Capture(capture);
            }
        }

        public ImuSample GetImuSample(int timeout_in_ms = -1)
        {
            lock (this)
            {
                if (disposedValue)
                    throw new ObjectDisposedException(nameof(Device));

                NativeMethods.k4a_wait_result_t result = NativeMethods.k4a_device_get_imu_sample(handle, out ImuSample sample, timeout_in_ms);

                if (result == NativeMethods.k4a_wait_result_t.K4A_WAIT_RESULT_TIMEOUT)
                {
                    throw new TimeoutException("Timed out waiting for imu sample");
                }

                Exception.ThrowIfNotSuccess(result);

                return sample;
            }
        }


        public Int32 GetColorControl(ColorControlCommand command)
        {
            lock (this)
            {
                if (disposedValue)
                    throw new ObjectDisposedException(nameof(Device));

                return this.GetColorControl(command, out ColorControlMode mode);
            }
        }

        public Int32 GetColorControl(ColorControlCommand command, out ColorControlMode mode)
        {
            lock (this)
            {
                if (disposedValue)
                    throw new ObjectDisposedException(nameof(Device));

                Exception.ThrowIfNotSuccess(NativeMethods.k4a_device_get_color_control(handle, command, out mode, out int value));
                return value;
            }
        }

        public void SetColorControl(ColorControlCommand command, ColorControlMode mode, Int32 value)
        {
            lock (this)
            {
                if (disposedValue)
                    throw new ObjectDisposedException(nameof(Device));

                Exception.ThrowIfNotSuccess(NativeMethods.k4a_device_set_color_control(handle, command, mode, value));
            }
        }

        public bool SyncInJackConnected
        {
            get
            {
                lock (this)
                {
                    if (disposedValue)
                        throw new ObjectDisposedException(nameof(Device));

                    Exception.ThrowIfNotSuccess(NativeMethods.k4a_device_get_sync_jack(handle,
                        out bool sync_in,
                        out bool sync_out));
                    return sync_in;
                }
            }
        }

        public bool SyncOutJackConnected
        {
            get
            {
                lock (this)
                {
                    if (disposedValue)
                        throw new ObjectDisposedException(nameof(Device));

                    Exception.ThrowIfNotSuccess(NativeMethods.k4a_device_get_sync_jack(handle,
                        out bool sync_in,
                        out bool sync_out));
                    return sync_out;
                }
            }
        }

        // Cache the version information so we don't need to re-marshal it for each
        // access since it is not allowed to change
        private HardwareVersion? version = null;

        public HardwareVersion Version
        {
            get
            {
                lock (this)
                {
                    if (disposedValue)
                        throw new ObjectDisposedException(nameof(Device));

                    if (version != null)
                        return version.Value;

                    Exception.ThrowIfNotSuccess(NativeMethods.k4a_device_get_version(handle,
                        out NativeMethods.k4a_hardware_version_t nativeVersion));

                    version = nativeVersion.ToHardwareVersion();
                    return version.Value;
                }
            }
        }

        public void StartCameras(DeviceConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            lock (this)
            {
                if (disposedValue)
                    throw new ObjectDisposedException(nameof(Device));

                Exception.ThrowIfNotSuccess(NativeMethods.k4a_device_start_cameras(handle, configuration.GetNativeConfiguration()));
            }
        }

        public void StopCameras()
        {
            lock (this)
            {
                if (disposedValue)
                    throw new ObjectDisposedException(nameof(Device));

                NativeMethods.k4a_device_stop_cameras(handle);
            }
        }

        public void StartImu()
        {
            lock (this)
            {
                if (disposedValue)
                    throw new ObjectDisposedException(nameof(Device));

                Exception.ThrowIfNotSuccess(NativeMethods.k4a_device_start_imu(handle));
            }
        }

        public void StopImu()
        {
            lock (this)
            {
                if (disposedValue)
                    throw new ObjectDisposedException(nameof(Device));

                NativeMethods.k4a_device_stop_imu(handle);
            }
        }

        private NativeMethods.k4a_device_t handle;

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.
                handle.Close();
                handle = null;

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        ~Device() {
          // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
          Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}