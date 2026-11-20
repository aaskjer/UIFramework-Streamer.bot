using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>The colour picker's own boxes: the hex code and the numbers of red, green and blue are typed into,
    /// and what is typed moves the picker and goes into the field.</summary>
    public class ColourPickerTests
    {
        private static void Sta(Action body)
        {
            Exception failure = null;
            var thread = new Thread(() => { try { body(); } catch (Exception ex) { failure = ex; } });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }

        private static IEnumerable<T> Find<T>(object node) where T : class
        {
            if (node is T hit) yield return hit;
            if (node is DependencyObject d)
                foreach (var child in LogicalTreeHelper.GetChildren(d))
                    foreach (var found in Find<T>(child))
                        yield return found;
        }

        /// <summary>An open picker over a field's box that holds the given text.</summary>
        private sealed class Opened : IDisposable
        {
            public readonly TextBox Field;
            public readonly Popup Popup;
            public readonly TextBox Hex, R, G, B;

            public Opened(string stored)
            {
                Field = new TextBox { Text = stored };
                var picker = new ColourPicker();
                picker.Open(Field, Field);
                Popup = (Popup)typeof(ColourPicker).GetField("_popup", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(picker);
                TextBox Box(string name) => Find<TextBox>(Popup.Child).Single(t => t.Name == name);
                Hex = Box("HexBox"); R = Box("ValueR"); G = Box("ValueG"); B = Box("ValueB");
            }

            public string Channels => R.Text + "," + G.Text + "," + B.Text;
            public static void Leave(TextBox box) => box.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent, box));
            public void Dispose() => Popup.IsOpen = false;
        }

        [Fact]
        public void TheHexCodeIsABoxThatShowsTheColourAndTakesOne() => Sta(() =>
        {
            using (var picker = new Opened("#112233"))
            {
                Assert.Equal("#112233", picker.Hex.Text);
                Assert.Equal("17,34,51", picker.Channels);
                Assert.Equal("#112233", picker.Field.Text);        // opening writes nothing

                picker.Hex.Text = "#F5A623";
                Assert.Equal("#F5A623", picker.Field.Text);        // into the field
                Assert.Equal("245,166,35", picker.Channels);       // and the picker moves along

                // As it is typed: without the #, in lower case. The box keeps what was typed until the cursor leaves it.
                picker.Hex.Text = "00ff00";
                Assert.Equal("#00FF00", picker.Field.Text);
                Assert.Equal("00ff00", picker.Hex.Text);
                Opened.Leave(picker.Hex);
                Assert.Equal("#00FF00", picker.Hex.Text);
            }
        });

        [Fact]
        public void TextThatIsNoColourYetChangesNothing() => Sta(() =>
        {
            using (var picker = new Opened("#112233"))
            {
                foreach (string partial in new[] { "", "#", "#11223", "nothing" })
                {
                    picker.Hex.Text = partial;
                    Assert.Equal("#112233", picker.Field.Text);
                    Assert.Equal("17,34,51", picker.Channels);
                    Assert.Equal(partial, picker.Hex.Text);         // nor is the typing overwritten
                }
                Opened.Leave(picker.Hex);
                Assert.Equal("#112233", picker.Hex.Text);           // the picker's colour again
            }
        });

        [Fact]
        public void AnAlphaTypedIntoTheHexBoxIsKept() => Sta(() =>
        {
            using (var picker = new Opened("#112233"))
            {
                picker.Hex.Text = "#80FF0000";
                Assert.Equal("#80FF0000", picker.Field.Text);
                picker.G.Text = "16";                               // a channel changed after it: the alpha stays
                Assert.Equal("#80FF1000", picker.Field.Text);
                picker.Hex.Text = "#102030";                        // none typed: opaque
                Assert.Equal("#102030", picker.Field.Text);
            }
        });

        [Fact]
        public void TheNumbersOfRedGreenAndBlueAreTyped() => Sta(() =>
        {
            using (var picker = new Opened("#000000"))
            {
                picker.R.Text = "245";
                picker.G.Text = "166";
                picker.B.Text = "35";
                Assert.Equal("#F5A623", picker.Field.Text);
                Assert.Equal("#F5A623", picker.Hex.Text);

                // On the way to "35": "3" is already a number, and "035" says 35 without being rewritten.
                picker.B.Text = "3";
                Assert.Equal("#F5A603", picker.Field.Text);
                picker.B.Text = "035";
                Assert.Equal("#F5A623", picker.Field.Text);
                Assert.Equal("035", picker.B.Text);
                Opened.Leave(picker.B);
                Assert.Equal("35", picker.B.Text);
            }
        });

        [Fact]
        public void ANumberAbove255Is255AndAnEmptyBoxChangesNothing() => Sta(() =>
        {
            using (var picker = new Opened("#102030"))
            {
                picker.R.Text = "300";
                Assert.Equal("255", picker.R.Text);
                Assert.Equal("#FF2030", picker.Field.Text);

                picker.G.Text = "";
                Assert.Equal("#FF2030", picker.Field.Text);
                Assert.Equal("", picker.G.Text);
                Opened.Leave(picker.G);
                Assert.Equal("32", picker.G.Text);

                Assert.Equal(3, picker.R.MaxLength);
                Assert.Equal(9, picker.Hex.MaxLength);              // "#AARRGGBB"
            }
        });

        [Fact]
        public void WhatIsTypedIntoTheFieldStillMovesThePickersBoxes() => Sta(() =>
        {
            using (var picker = new Opened("#102030"))
            {
                picker.Field.Text = "#F5A623";
                Assert.Equal("#F5A623", picker.Hex.Text);
                Assert.Equal("245,166,35", picker.Channels);
            }
        });

        [Fact]
        public void UpAndDownStepANumberByOne() => Sta(() =>
        {
            // A key event names the window it came through; any one does for a key raised by hand.
            using (var source = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("keys")))
            using (var picker = new Opened("#102030"))
            {
                void Press(TextBox box, Key key) => box.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = UIElement.PreviewKeyDownEvent });
                Press(picker.R, Key.Up);
                Assert.Equal("17", picker.R.Text);
                Assert.Equal("#112030", picker.Field.Text);
                Press(picker.B, Key.Down);
                Press(picker.B, Key.Down);
                Assert.Equal("#11202E", picker.Field.Text);
                // At the ends it stays.
                picker.G.Text = "255";
                Press(picker.G, Key.Up);
                Assert.Equal("255", picker.G.Text);
            }
        });
    }
}
