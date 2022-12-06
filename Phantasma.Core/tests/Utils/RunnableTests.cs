using System;
using System.Threading;

namespace Phantasma.Core.Tests.Utils;

using Xunit;
using Phantasma.Core.Utils;

public class RunnableTests
{
    // Create a class based on the Runnable class
    public class MyRunnable : Runnable
    {
        public int Counter { get; private set; }

        public MyRunnable()
        {
            Counter = 0;
        }

        protected override bool Run()
        {
            Counter++;
            Thread.Sleep(10);
            if ( Counter >= 50 )
            {
                return false;
            }
            return true;
        }
        
        protected override void OnStop()
        {
            Counter = 0;
        }
        
        protected override void OnStart()
        {
            Counter = 0;
        }
    }
    
    [Fact]
    public void TestRunnable2()
    {
        var runnable = new MyRunnable();
        runnable.StartInThread();
        // Wait for it to run at least 1 time
        Thread.Sleep(20);
        Assert.True(runnable.Counter > 0);
        runnable.Stop();
        Assert.Equal(0, runnable.Counter);
    }
}
