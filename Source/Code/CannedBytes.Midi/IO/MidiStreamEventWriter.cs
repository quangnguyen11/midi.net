using System;
using System.Diagnostics.Contracts;
using System.IO;

namespace CannedBytes.Midi.IO
{
    /// <summary>
    /// The MidiEventStreamWriter class writes short or long midi messages
    /// into a <see cref="MidiBufferStream"/>.
    /// </summary>
    public class MidiEventStreamWriter : DisposableBase
    {
        /// <summary>
        /// Constructs a new instance on the specified <paramref name="stream"/>.
        /// </summary>
        /// <param name="stream">A stream provided by a <see cref="MidiOutStreamPort"/>. Must not be null.</param>
        public MidiEventStreamWriter(MidiBufferStream stream)
        {
            #region Method Checks

            Throw.IfArgumentNull(stream, "stream");
            if (!stream.CanWrite)
            {
                throw new ArgumentException(
                    Properties.Resources.MidiStreamWriter_StreamNotWritable, "stream");
            }

            #endregion Method Checks

            BaseStream = stream;
            InnerWritter = new BinaryWriter(stream);
        }

        [ContractInvariantMethod]
        private void InvariantContract()
        {
            Contract.Invariant(BaseStream != null);
            Contract.Invariant(InnerWritter != null);
        }

        /// <summary>
        /// Gets a binary writer derived classes can use to write data to the stream.
        /// </summary>
        protected BinaryWriter InnerWritter { get; private set; }

        /// <summary>
        /// Gets the stream this writer is acting on.
        /// </summary>
        public MidiBufferStream BaseStream { get; private set; }

        /// <summary>
        /// Returns true if the stream has room to write one short midi message.
        /// </summary>
        /// <returns>Returns false if there is no more room.</returns>
        public bool CanWriteShort()
        {
            return CanWriteLong(null);
        }

        /// <summary>
        /// Checks if there is room to write the specified <paramref name="longMsg"/> into the stream.
        /// </summary>
        /// <param name="longMsg">A buffer containing the long midi message. Can be null.</param>
        /// <returns>Returns false if there is no more room.</returns>
        /// <remarks>If the <paramref name="longMsg"/> is null,
        /// the method checks the stream for a short midi message.</remarks>
        public bool CanWriteLong(byte[] longMsg)
        {
            ThrowIfDisposed();

            // size of a MidiEvent struct in the buffer
            int size = 3 * 4; // 3 integers each 4 bytes

            if (longMsg != null)
            {
                size += longMsg.Length;
            }

            return (BaseStream.Position + size < BaseStream.Capacity);
        }

        /// <summary>
        /// Writes a short midi message to the stream.
        /// </summary>
        /// <param name="shortMsg">The short midi message.</param>
        /// <param name="deltaTime">A time indication of the midi message.</param>
        public void WriteShort(int shortMsg, int deltaTime)
        {
            ThrowIfDisposed();

            MidiEventData data = new MidiEventData(shortMsg);

            data.EventType = MidiEventType.ShortMessage;

            WriteEvent(data.Data, deltaTime, null);
        }

        /// <summary>
        /// Writes a long midi message to the stream.
        /// </summary>
        /// <param name="longMsg">A buffer containing the long midi message.</param>
        /// <param name="deltaTime">A time indication of the midi message.</param>
        public void WriteLong(byte[] longMsg, int deltaTime)
        {
            Contract.Requires(longMsg != null);
            ThrowIfDisposed();
            Throw.IfArgumentNull(longMsg, "longMsg");

            MidiEventData data = new MidiEventData();
            data.Length = longMsg.Length;
            data.EventType = MidiEventType.LongMessage;

            WriteEvent(data, deltaTime, longMsg);
        }

        /// <summary>
        /// Writes a midi event to stream.
        /// </summary>
        /// <param name="midiEvent">The midi event data.</param>
        /// <param name="deltaTime">A time indication of the midi event.</param>
        /// <param name="longData">Optional long message data. Can be null.</param>
        /// <remarks>Refer to the Win32 MIDIEVNT structure for more information.</remarks>
        public void WriteEvent(int midiEvent, int deltaTime, byte[] longData)
        {
            #region Method Checks

            ThrowIfDisposed();
            if (!CanWriteLong(longData))
            {
                throw new MidiStreamException(Properties.Resources.MidiStream_EndOfStream);
            }

            #endregion Method Checks

            InnerWritter.Write(deltaTime);
            InnerWritter.Write(0);	// streamID
            InnerWritter.Write(midiEvent);

            if (longData != null)
            {
                InnerWritter.Write(longData, 0, longData.Length);
            }
        }

        /// <summary>
        /// Disposes the base stream.
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    BaseStream.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}