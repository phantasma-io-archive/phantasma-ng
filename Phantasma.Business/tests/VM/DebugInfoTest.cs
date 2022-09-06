using System;
using System.Collections.Generic;
using Phantasma.Business.VM;
using Shouldly;
using Xunit;

namespace Phantasma.Business.Tests.VM;


public class DebugInfoTest
{
    private List<DebugRange> emptyDebugRangeList;
    private List<DebugRange> debugRangeList;
    

    private string filename;
    private DebugInfo debugInfo;
    
    public void Init()
    {
        emptyDebugRangeList = new List<DebugRange>();
        debugRangeList = new List<DebugRange>();
        var debugRange = new DebugRange(2,2,4);
        debugRangeList.Add(debugRange);
        filename = "testFile.txt";
    }
    
    [Fact]
    public void null_debugRange_test()
    {
        // Arrange
        var debugRange = new DebugRange();
        
        // Act
        
        // Assert
        debugRange.SourceLine.ShouldBe((uint)0);
        debugRange.StartOffset.ShouldBe(0);
        debugRange.EndOffset.ShouldBe(0);
    }

    [Fact]
    public void someValue_debugRange_test()
    {
        // Arrange
        var debugRange = new DebugRange(1,2,3);
        
        // Assert
        debugRange.SourceLine.ShouldBe((uint)1);
        debugRange.StartOffset.ShouldBe(2);
        debugRange.EndOffset.ShouldBe(3);
    }

    [Fact]
    public void null_debugInfo_test()
    {
        // Arrange
        DebugInfo debugInfo = null;
        
        // Assert
        debugInfo.ShouldBeNull();
    }

    [Fact]
    public void someValue_debugInfo_test()
    {
        // Arrange
        Init();
        debugInfo = new DebugInfo( filename, emptyDebugRangeList );

        
        // Assert
        debugInfo.Ranges.ShouldBe(emptyDebugRangeList);
        debugInfo.FileName.ShouldBe(filename);
    }

    [Fact]
    public void findline_debugInfo_test()
    {
        // Arrange
        Init();
        debugInfo = new DebugInfo( filename, debugRangeList );
        
        // Act
        var result = debugInfo.FindLine(-1);
        var result2 = debugInfo.FindLine(2);
        
        // Assert
        result.ShouldBe(-1);
        result2.ShouldBe(2);
    }
    
    [Fact]
    public void findoffset_debugInfo_test()
    {
        // Arrange
        Init();
        debugInfo = new DebugInfo( filename, debugRangeList );

        // Act
        var result = debugInfo.FindOffset(0);
        var result2 = debugInfo.FindOffset(2);
        
        // Assert
        result.ShouldBe(-1);
        result2.ShouldBe(2);
    }

    [Fact]
    public void tobytearray_debugInfo_test()
    {
        // Arrange
        Init();
        debugInfo = new DebugInfo( filename, debugRangeList );
        byte[] expected = new byte[] {12, 116, 101, 115, 116, 70, 105, 108, 101, 46, 116, 120, 116, 1, 0, 0, 0, 2, 0, 0, 0, 2, 0, 0, 0, 4, 0, 0, 0};
        
        // Act
        var result = debugInfo.ToByteArray();

        // Assert
        result.ShouldBe(expected);
    }
    
    [Fact]
    public void tojson_debugInfo_test()
    {
        // Arrange
        Init();
        debugInfo = new DebugInfo( filename, debugRangeList );
        
        // Act
        var result = debugInfo.ToJSON();

        // Assert
        result.ShouldMatch("{}");
    }

}
