/*
 * StreamExtensions
 * CorelibNet.Gettext
 *
 * Copyright 2013 Enscope, s.r.o. <dev@enscope.com>
 *     Miroslav Hudak <mhudak@enscope.com>
 *     
 * Portions copyright their respective authors.
 *
 * License: Attribution-ShareAlike 3.0 Unported (CC BY-SA 3.0)
 *          http://creativecommons.org/licenses/by-sa/3.0/
 */

using System.IO;
using System.Runtime.InteropServices;

namespace CorelibNet.Gettext.Utilities
{
    // See: http://stackoverflow.com/a/4159279
    public static class StreamExtensions
    {
        public static T ReadStruct<T>(this Stream stream) 
            where T : struct
        {
            var sz = Marshal.SizeOf(typeof(T));
            var buffer = new byte[sz];
            stream.Read(buffer, 0, sz);
            var pinnedBuffer = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            var structure = (T)Marshal.PtrToStructure(
                pinnedBuffer.AddrOfPinnedObject(), typeof(T));
            pinnedBuffer.Free();
            return (structure);
        }
    }
}
