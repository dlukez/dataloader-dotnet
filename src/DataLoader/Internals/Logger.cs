using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DataLoader
{
    internal static class Logger
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteLine(string message)
        {
            Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(2, ' ')} / Task {Task.CurrentId.ToString().PadLeft(4, ' ')} - {message}");
        }
    }
}