using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;

namespace Phantasma.Core.Tests;

public class ExtensionMethodsTests
{
    [Theory]
    [InlineData(typeof(TestStruct))]
    [InlineData(typeof(ExtensionMethodsTests))]
    public void IsStructOrClass_should_return_true_if_struct_or_class(Type type)
    {
        // Act
        var result = type.IsStructOrClass();

        // Assert
        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData(typeof(string))]
    [InlineData(typeof(int))]
    public void IsStructOrClass_should_return_false_if_not_struct_or_class(Type type)
    {
        // Act
        var result = type.IsStructOrClass();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void AsByteArray_should_return_byte_array()
    {
        // Arrange
        var test = "test";

        // Act
        var result = test.AsByteArray();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(new byte[] { 116, 101, 115, 116 });
    }

    [Fact]
    public void AsString_should_return_string_from_byte_array()
    {
        // Arrange
        var test = new byte[] { 116, 101, 115, 116 };

        // Act
        var result = test.AsString();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe("test");
    }

    [Fact]
    public void ForEachGeneric_should_apply_action_to_each_item_in_collection()
    {
        // Arrange
        var collection1 = new List<string> { "test1", "test2", "test3" };
        var collection2 = new List<string>();

        // Act
        collection1.ForEach<string>(s => { collection2.Add(s); });

        // Assert
        collection2.ShouldNotBeEmpty();
        collection2.ShouldBeEquivalentTo(collection1);
    }

    [Fact]
    public void ForEachWithIndexGeneric_should_apply_action_to_each_item_in_collection()
    {
        // Arrange
        var collection1 = new List<string> { "test1", "test2", "test3" };
        var collection2 = new List<string>();

        // Act
        collection1.ForEachWithIndex((s, i) => { collection2.Add($"{s}{i}"); });

        // Assert
        collection2.ShouldNotBeEmpty();
        collection2.ShouldBeEquivalentTo(new List<string> { "test10", "test21", "test32" });
    }

    [Fact]
    public void ForEachNonGenericEnumerators_should_apply_action_to_each_item_in_collection()
    {
        // Arrange
        var collection1 = (IEnumerable)new List<string> { "test1", "test2", "test3" };
        var collection2 = new List<string>();

        // Act
        collection1.ForEach<string>(s => collection2.Add(s));

        // Assert
        collection2.ShouldNotBeEmpty();
        collection2.ShouldBeEquivalentTo(collection1);
    }

    [Fact]
    public void ForEachInt_should_apply_action_on_each_iteration()
    {
        // Arrange
        var iterations = 5;
        var count = 0;

        // Act
        iterations.ForEach(() => count++);

        // Assert
        count.ShouldBe(5);
    }

    [Fact]
    public void ForEachIntWithIndex_should_apply_action_on_each_iteration()
    {
        // Arrange
        var iterations = 5;
        var collection = new List<int>();

        // Act
        iterations.ForEach(i => collection.Add(i));

        // Assert
        collection.ShouldNotBeEmpty();
        collection.Count.ShouldBe(5);
    }

    [Fact]
    public void Range_should_create_enumerable_of_specified_size()
    {
        // Arrange
        var iterations = 5;

        // Act
        var result = iterations.Range();

        // Assert
        result.ShouldNotBeEmpty();
        result.Count().ShouldBe(5);
    }

    [Fact]
    public void MoveToTail_should_remove_the_item_and_add_it_to_end_of_list_when_found()
    {
        // Arrange
        var collection = new List<string> { "test1", "test2", "test3" };

        // Act
        collection.MoveToTail("test1", s => s == "test1");

        // Assert
        collection.ShouldNotBeEmpty();
        collection.Count.ShouldBe(3);
        collection.ShouldBeEquivalentTo(new List<string> { "test2", "test3", "test1" });
    }

    [Fact]
    public void MoveToTail_should_leave_list_unchanged_when_not_found()
    {
        // Arrange
        var collection = new List<string> { "test1", "test2", "test3" };

        // Act
        collection.MoveToTail("test4", s => s == "test4");

        // Assert
        collection.ShouldNotBeEmpty();
        collection.Count.ShouldBe(3);
        collection.ShouldBeEquivalentTo(new List<string> { "test1", "test2", "test3" });
    }

    [Fact]
    public void AddMaximum_should_remove_oldest_entry_when_max_size_is_reached()
    {
        // Arrange
        var collection = new List<string> { "test1", "test2", "test3" };

        // Act
        collection.AddMaximum("test4", 3);

        // Assert
        collection.ShouldNotBeEmpty();
        collection.Count.ShouldBe(3);
        collection.ShouldBeEquivalentTo(new List<string> { "test2", "test3", "test4" });
    }

    [Fact]
    public void AddDistinct_should_not_allow_duplicates()
    {
        // Arrange
        var collection = new List<string> { "test1" };

        // Act
        collection.AddDistinct("test1");
        collection.AddDistinct("test2");

        // Assert
        collection.ShouldNotBeEmpty();
        collection.Count.ShouldBe(2);
        collection.ShouldBeEquivalentTo(new List<string> { "test1", "test2" });
    }

    [Fact]
    public void RemoveRange_should_remove_matching_source_items()
    {
        // Arrange
        var collection1 = new List<string> { "test1", "test2", "test3" };
        var collection2 = new List<string> { "test1", "test3" };

        // Act
        collection1.RemoveRange(collection2);

        // Assert
        collection1.ShouldNotBeEmpty();
        collection1.Count.ShouldBe(1);
        collection1.ShouldBeEquivalentTo(new List<string> { "test2" });
    }

    [Fact]
    public void RemoveRange_should_remove_matching_source_items_using_custom_equality_comparer()
    {
        // Arrange
        var collection1 = new List<string> { "test1", "test2", "test3" };
        var collection2 = new List<string> { "test1", "test3", "test5" };

        // Act
        collection1.RemoveRange(collection2, (s1, s2) => s1 == s2);

        // Assert
        collection1.ShouldNotBeEmpty();
        collection1.Count.ShouldBe(1);
        collection1.ShouldBeEquivalentTo(new List<string> { "test2" });
    }

    [Fact]
    public void ApproximatelyEquals_should_return_true_when_within_range()
    {
        // Arrange
        var source = 1.25;
        var value = 1.35;

        // Act
        var result = source.ApproximatelyEquals(value, 0.15);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ApproximatelyEquals_should_return_false_when_not_within_range()
    {
        // Arrange
        var source = 1.25;
        var value = 1.35;

        // Act
        var result = source.ApproximatelyEquals(value, 0.05);

        // Assert
        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData(new[] { 1d, 2, 3, 4, 5 }, 1.58113883, 1.58113884)]
    [InlineData(new[] { 4d, 2, 5, 8, 6 }, 2.23606797, 2.23606798)]
    public void StdDev_should_return_the_standard_deviation_of_the_collection_values(double[] collection,
        double expectedFrom, double expectedTo)
    {
        // Act
        var result = collection.StdDev();

        // Assert
        result.ShouldBeInRange(expectedFrom, expectedTo);
    }

    [Fact]
    public void Shuffle_should_arrange_array_randomly()
    {
        // Arrange
        var collection = new List<string> { "test1", "test2", "test3" };

        // Act
        collection.Shuffle();

        // Assert
        collection.ShouldNotBeEmpty();
        collection.Count.ShouldBe(3);
        collection.ShouldBeSubsetOf(new List<string> { "test1", "test2", "test3" });
    }

    [Fact]
    public void Merge_should_merge_dictionaries()
    {
        // Arrange
        var dictionary1 = new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } };
        var dictionary2 =
            new Dictionary<string, string> { { "key1", "value1" }, { "key3", "value3" }, { "key4", "value4" } };

        // Act
        dictionary2.Merge(dictionary1);

        // Assert
        dictionary2.ShouldNotBeEmpty();
        dictionary2.Count.ShouldBe(4);
        dictionary2.ShouldContainKeyAndValue("key1", "value1");
        dictionary2.ShouldContainKeyAndValue("key2", "value2");
        dictionary2.ShouldContainKeyAndValue("key3", "value3");
        dictionary2.ShouldContainKeyAndValue("key4", "value4");
    }
    
    [Fact]
    public void ContainsBy_should_dictionaries()
    {
        // Arrange
        var myList = new List<string> {  "key1", "key2", "key3" };
        
        // Act

        // Assert
        Assert.True(myList.ContainsBy("key1", s => s == "key1"));
    }
    
    [Fact]
    public void AddDistinctBy_should_dictionaries()
    {
        // Arrange
        var myList = new List<string> {  "key1", "key2", "key3" };
        
        // Act
        myList.AddDistinctBy("key4", s => s == "key4");
        
        // Assert
        Assert.True(myList.ContainsBy("key4", s => s == "key4"));
    }
    
    [Fact]
    public void AddRangeDistinctBy_should_dictionaries()
    {
        // Arrange
        var myList = new List<string> {  "key1", "key2", "key3" };
        
        // Act
        myList.AddRangeDistinctBy(new List<string> { "key4", "key5" }, (src, succes) =>
        {
             return (src.Contains("key4"));
        });
        
        // Assert
        Assert.True(myList.ContainsBy("key4", s => s == "key4"));
    }
    
    [Fact]
    public void None_should_dictionaries()
    {
        // Arrange
        var myList = new List<string> {  "key1", "key2", "key3" };
        
        // Act
        myList.None((src) =>
        {
            return (src.Contains("key34"));
        });
        
        // Assert
        Assert.True(myList.ContainsBy("key3", s => s == "key3"));
    }

    [Theory]
    [InlineData(new[] { "test1" }, false)]
    [InlineData(new string[] { }, true)]
    public void NoneGeneric_should_return_true_if_empty(string[] collection, bool expected)
    {
        // Act
        var result = collection.None();

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void NoneGeneric_should_return_true_if_no_matches_exist_for_predicate()
    {
        // Arrange
        var collection = new List<string> { "test1", "test2", "test3" };

        // Act
        var result = collection.None(s => s == "test4");

        // Assert
        result.ShouldBeTrue();
    }

    private struct TestStruct
    {
    }

    private enum TestEnum
    {
        Test
    }
}
