namespace CannedBytes.Midi
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Diagnostics.Contracts;
    using System.Globalization;
    using System.Runtime.InteropServices;

    /// <summary>
    /// The MidiPort class represents an abstract base class for concrete
    /// Midi Port implementations.
    /// </summary>
    /// <remarks>The MidiPort implements Methods and Properties common to all
    /// Midi Port implementations.</remarks>
    public abstract class MidiPort : DisposableBase, IMidiPort
    {
        /// <summary>
        /// A handle to this instance that is passed to unmanaged functions.
        /// </summary>
        private GCHandle instanceHandle;

        /// <summary>
        /// For derived classes only.
        /// </summary>
        protected MidiPort()
        {
            this.status = MidiPortStatus.Closed;
            this.instanceHandle = GCHandle.Alloc(this, GCHandleType.Weak);
            this.AutoReturnBuffers = true;
        }

        /// <summary>
        /// Queries the port <see cref="P:Status"/> if one or more of the
        /// specified <see cref="MidiPortStatus"/> flags are present.
        /// </summary>
        /// <param name="portStatus">One or more status flags to query.</param>
        /// <returns>Returns true if one or more of the <see cref="MidiPortStatus"/>
        /// flags is set in the <see cref="P:Status"/> property.</returns>
        public bool HasStatus(MidiPortStatus portStatus)
        {
            return (this.Status & portStatus) > 0;
        }

        /// <summary>
        /// Modifies the <see cref="P:Status"/> value.
        /// </summary>
        /// <param name="addStatus">One or more status flags to add.</param>
        /// <param name="removeStatus">One or more status flags to remove.</param>
        /// <exception cref="MidiPortException">Thrown when the resulting status would be invalid.</exception>
        /// <remarks>
        /// The following status flag combinations are invalid:
        /// <see cref="P:Status"/> cannot equal <see cref="MidiPortStatus.None"/>.
        /// <see cref="P:Status"/> cannot contain any other flags when set to <see cref="MidiPortStatus.Closed"/>.
        /// <see cref="P:Status"/> cannot contain both <see cref="MidiPortStatus.Started"/> and
        /// <see cref="MidiPortStatus.Stopped"/> or <see cref="MidiPortStatus.Paused"/>.
        /// </remarks>
        protected void ModifyStatus(MidiPortStatus addStatus, MidiPortStatus removeStatus)
        {
            this.ThrowIfDisposed();

            MidiPortStatus portStatus = this.Status;

            // automatically clear the Reset status.
            portStatus &= ~(removeStatus | MidiPortStatus.Reset);
            portStatus |= addStatus;

            // validate modifications
            if (portStatus == MidiPortStatus.None)
            {
                throw new MidiPortException(
                    String.Format(CultureInfo.InvariantCulture, Properties.Resources.MidiPort_InvalidStatus, portStatus));
            }

            // closed cannot be combined with another status
            if (((portStatus & MidiPortStatus.Closed) > 0) &&
                (portStatus != MidiPortStatus.Closed))
            {
                throw new MidiPortException(
                    String.Format(CultureInfo.InvariantCulture, Properties.Resources.MidiPort_InvalidStatus, portStatus));
            }

            // cannot be started and stopped or paused at the same time.
            if (((portStatus & MidiPortStatus.Started) > 0) &&
                (((portStatus & MidiPortStatus.Stopped) > 0) || ((portStatus & MidiPortStatus.Paused) > 0)))
            {
                throw new MidiPortException(
                    String.Format(CultureInfo.InvariantCulture, Properties.Resources.MidiPort_InvalidStatus, portStatus));
            }

            this.Status = portStatus;
        }

        #region IMidiPort Members

        /// <summary>
        /// The backing field for the <see cref="Status"/> property.
        /// </summary>
        private MidiPortStatus status;

        /// <summary>
        /// Returns the current status of the Midi Port.
        /// </summary>
        public MidiPortStatus Status
        {
            get
            {
                return this.status;
            }

            internal set
            {
                this.ThrowIfDisposed();

                if (this.status != value)
                {
                    this.status = value;

                    this.OnStatusChanged(EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// The backing field for the <see cref="PortId"/> property.
        /// </summary>
        private int? portId;

        /// <summary>
        /// Gets the identifier of the Midi Port.
        /// </summary>
        /// <remarks>Returns the same id as passed in the <see cref="M:Open"/> method.</remarks>
        public int PortId
        {
            get { return this.portId.GetValueOrDefault(-1); }
        }

        /// <summary>
        /// Set this to false when you want to return buffers manually.
        /// </summary>
        public bool AutoReturnBuffers { get; set; }

        /// <summary>
        /// Opens the Midi Port identified by the <paramref name="deviceId"/>.
        /// </summary>
        /// <param name="deviceId">An index into the available port list.</param>
        /// <remarks>
        /// The <see cref="P:Status"/> property will be set to <see cref="MidiPortStatus.Open"/>.
        /// </remarks>
        public virtual void Open(int deviceId)
        {
            this.portId = deviceId;

            this.Status = MidiPortStatus.Open;

            if (!this.IsOpen)
            {
                this.Status |= MidiPortStatus.Pending;
            }
        }

        /// <summary>
        /// Gets a value indicating if the <see cref="MidiSafeHandle"/> is set.
        /// </summary>
        public virtual bool IsOpen
        {
            get { return MidiSafeHandle != null && !MidiSafeHandle.IsInvalid && !MidiSafeHandle.IsClosed; }
        }

        /// <summary>
        /// Closes the Midi Port.
        /// </summary>
        public virtual void Close()
        {
            if (MidiSafeHandle != null)
            {
                this.Status = MidiPortStatus.Closed | MidiPortStatus.Pending;

                MidiSafeHandle.Close();
                MidiSafeHandle = null;
            }

            this.portId = null;
        }

        /// <summary>
        /// Resets the Midi Port.
        /// </summary>
        public virtual void Reset()
        {
            this.ModifyStatus(MidiPortStatus.Reset, MidiPortStatus.None);
        }

        /// <summary>
        /// The event is raised after the <see cref="P:Status"/> of the Midi Port has changed.
        /// </summary>
        public event EventHandler StatusChanged;

        /// <summary>
        /// Raises the <see cref="StatusChanged"/> event.
        /// </summary>
        /// <param name="e">Pass <see cref="EventArgs.Empty"/>.</param>
        protected virtual void OnStatusChanged(EventArgs e)
        {
            var handler = this.StatusChanged;

            if (handler != null)
            {
                handler(this, e);
            }
        }

        #endregion IMidiPort Members

        /// <summary>
        /// Connects this Midi Port to the specified <paramref name="outPort"/>.
        /// </summary>
        /// <param name="outPort">A reference to a Midi Out Port. Must not be null.</param>
        [SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "0", Justification = "Check is not recognized.")]
        public virtual void Connect(MidiOutPort outPort)
        {
            Contract.Requires(outPort != null);
            Check.IfArgumentNull(outPort, "outPort");
            this.ThrowIfDisposed();

            int result = NativeMethods.midiConnect(MidiSafeHandle, outPort.MidiSafeHandle, IntPtr.Zero);

            MidiOutPort.ThrowIfError(result);
        }

        /// <summary>
        /// Disconnects this Midi Port from the specified <paramref name="outPort"/>.
        /// </summary>
        /// <param name="outPort">A reference to a Midi Out Port. Must not be null.</param>
        [SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "0", Justification = "Check is not recognized.")]
        public virtual void Disconnect(MidiOutPort outPort)
        {
            Contract.Requires(outPort != null);
            Check.IfArgumentNull(outPort, "outPort");
            this.ThrowIfDisposed();

            int result = NativeMethods.midiDisconnect(MidiSafeHandle, outPort.MidiSafeHandle, IntPtr.Zero);

            MidiOutPort.ThrowIfError(result);
        }

        #region IDisposable Members

        /// <summary>
        /// Closes the Midi Port (if needed) and disposes the instance.
        /// </summary>
        /// <param name="disposeKind">The type of resources to dispose.</param>
        /// <remarks>
        /// If <paramref name="disposeKind"/> is set to Managed the <see cref="P:BufferManager"/> is also disposed.
        /// </remarks>
        protected override void Dispose(DisposeObjectKind disposeKind)
        {
            if (!IsDisposed)
            {
                try
                {
                    if (this.Status != MidiPortStatus.Closed)
                    {
                        this.Close();
                    }
                }
                catch (MidiException e)
                {
                    // do nothing
                    Debug.WriteLine(e);
                }

                if (disposeKind == DisposeObjectKind.ManagedAndUnmanagedResources)
                {
                    this.instanceHandle.Free();
                }

                this.Status = MidiPortStatus.None;
            }
        }

        /// <summary>
        /// Check helper method for derived classes to throw an <see cref="ObjectDisposedException"/>
        /// exception when the instance has been disposed.
        /// </summary>
        protected override void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(String.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    Properties.Resources.MidiPort_ObjectDisposed,
                    GetType().FullName));
            }
        }

        #endregion IDisposable Members

        /// <summary>
        /// Gets the <see cref="SafeHandle"/> for the Midi Port. Can be null (if the port is not open).
        /// </summary>
        /// <remarks>Derived classes can access backing field usually during <see cref="Open"/> execution.</remarks>
        public MidiSafeHandle MidiSafeHandle { get; protected set; }

        /// <summary>
        /// Returns an <see cref="IntPtr"/> that represents the instance's this reference.
        /// </summary>
        /// <returns>Returns the instance <see cref="IntPtr"/>.</returns>
        /// <remarks>Dereference using <see cref="GCHandle"/>.</remarks>
        public IntPtr ToIntPtr()
        {
            this.ThrowIfDisposed();

            return GCHandle.ToIntPtr(this.instanceHandle);
        }

        /// <summary>
        /// Callback from the midi driver (on a separate thread).
        /// </summary>
        /// <param name="handle">Port handle.</param>
        /// <param name="msg">The midi message to handle.</param>
        /// <param name="instance">A <see cref="GCHandle"/> that contains a weak
        /// reference to the port instance.</param>
        /// <param name="param1">Parameter 1.</param>
        /// <param name="param2">Parameter 2.</param>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "We don't want to leak any exceptions into the Win32 API.")]
        private static void MidiProc(IntPtr handle, uint msg, IntPtr instance, IntPtr param1, IntPtr param2)
        {
            Contract.Requires(instance != IntPtr.Zero);

            try
            {
                GCHandle instanceHandle = GCHandle.FromIntPtr(instance);

                if (instanceHandle != null && instanceHandle.Target != null)
                {
                    ((MidiPort)instanceHandle.Target).OnMessage((int)msg, param1, param2);
                }
            }
            catch (Exception e)
            {
                // Do not leak any exceptions into the calling windows code.

                // TODO: log error
                Debug.WriteLine(e);
            }
        }

        /// <summary>
        /// Keeps a reference to the midi proc. delegate to avoid GC from taking it.
        /// </summary>
        internal static readonly NativeMethods.MidiProc MidiProcRef = MidiProc;

        /// <summary>
        /// Derived classes implement this method to process port messages.
        /// </summary>
        /// <param name="message">The port message.</param>
        /// <param name="parameter1">Message specific parameter 1.</param>
        /// <param name="parameter2">Message specific parameter 2.</param>
        /// <returns>Returns true when the message is handled.</returns>
        protected abstract bool OnMessage(int message, IntPtr parameter1, IntPtr parameter2);
    }
}