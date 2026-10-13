// Prisma.Tests - entry point of the test program. It is never shipped: the tests live outside Prisma.exe so the
// released program carries no test code and none of the strings the tests look for. It loads the real Prisma.exe as
// a library, so the tests exercise exactly the code (and inspect exactly the file) that is released.
using System;

namespace Prisma
{
    public static class TestMain
    {
        [STAThread]
        public static int Main(string[] args)
        {
            return SelfTest.Run();
        }
    }
}
