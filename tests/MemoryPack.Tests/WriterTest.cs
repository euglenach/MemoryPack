﻿using System;
using System.Buffers;

namespace MemoryPack.Tests;

public class WriterTest
{
    [Fact]
    public void BufferManagementTest()
    {
        var buffer = new SpanControlWriter();

        var writer = new MemoryPackWriter<SpanControlWriter>(ref buffer);

        buffer.ProvideSpanLength = 5;

        writer.GetSpanReference(3);
        writer.Advance(3);

        buffer.SpanRequested.Should().Be(1);
        buffer.AdvancedLength.Should().Be(0);

        writer.GetSpanReference(2);
        writer.Advance(2);

        buffer.SpanRequested.Should().Be(1);
        buffer.AdvancedLength.Should().Be(0);

        // request more span
        writer.GetSpanReference(3);
        buffer.SpanRequested.Should().Be(2);
        buffer.AdvancedLength.Should().Be(5);

        // invalid advance
        var error = false;
        try
        {
            writer.Advance(9999);
        }
        catch (InvalidOperationException)
        {
            error = true;
        }
        error.Should().BeTrue();
    }

    [Fact]
    public void WriteObjectHeaderTest()
    {
        var buffer = new ArrayBufferWriter<byte>();

        {
            var writer = new MemoryPackWriter<ArrayBufferWriter<byte>>(ref buffer);

            writer.WriteNullObjectHeader();
            writer.Flush();

            buffer.WrittenSpan[0].Should().Be(MemoryPackCode.NullObject);
            buffer.Clear();
        }
        for (var i = 0; i < 250; i++)
        {
            var writer = new MemoryPackWriter<ArrayBufferWriter<byte>>(ref buffer);
            writer.WriteObjectHeader((byte)i);
            writer.Flush();

            buffer.WrittenSpan[0].Should().Be((byte)i);
            buffer.Clear();
        }

        for (byte i = MemoryPackCode.Reserved1; i <= MemoryPackCode.NullObject; i++)
        {
            if (i == 0) break;
            var writer = new MemoryPackWriter<ArrayBufferWriter<byte>>(ref buffer);
            var error = false;
            try
            {
                writer.WriteObjectHeader((byte)i);
            }
            catch (InvalidOperationException)
            {
                error = true;
            }
            finally
            {
                buffer.Clear();
            }
            error.Should().BeTrue();
        }
    }


    public class SpanControlWriter : IBufferWriter<byte>
    {
        public int ProvideSpanLength { get; set; }
        public int AdvancedLength { get; private set; }
        public int SpanRequested { get; private set; }

        public void Advance(int count)
        {
            AdvancedLength += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            throw new NotImplementedException();
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            SpanRequested++;
            return new byte[Math.Max(sizeHint, ProvideSpanLength)];
        }
    }
}