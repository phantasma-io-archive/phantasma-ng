using System;
using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace Phantasma.Business.Tests.VM;

public class DebugInfoTest
{
    [Fact]
    public void null_debugRange_test()
    {
        var debugRange = new DebugRange();
        debugRange.SourceLine.ShouldBe((uint)0);
        debugRange.StartOffset.ShouldBe(0);
        debugRange.EndOffset.ShouldBe(0);
    }

    [Fact]
    public void someValue_debugRange_test()
    {
        var debugRange = new DebugRange(1,2,3);
        debugRange.SourceLine.ShouldBe((uint)1);
        debugRange.StartOffset.ShouldBe(2);
        debugRange.EndOffset.ShouldBe(3);
    }

    [Fact]
    public void null_debugInfo_test()
    {
        DebugInfo debugInfo = null;
        debugInfo.ShouldBeNull();
    }

    [Fact]
    public void someValue_debugInfo_test()
    {
        var emptyDebugRangeList = new List<DebugRange>();
        var filename = "testFile.txt";
        var debugInfo = new DebugInfo(filename, emptyDebugRangeList );
        debugInfo.Ranges.ShouldBe(emptyDebugRangeList);
        debugInfo.FileName.ShouldBe(filename);
    }
}
