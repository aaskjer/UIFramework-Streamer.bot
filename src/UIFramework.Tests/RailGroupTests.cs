using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>Groups of pages in the rail (UiWindow.Group): where a group and the pages in none sit, folding and where
    /// it is kept, what unfolds a group, the search, the customise mode, a locked window, the heading's translation,
    /// what a declaration can get wrong, and a window without groups.</summary>
    public class RailGroupTests : IDisposable
    {
        private readonly List<string> _folders = new List<string>();

        public void Dispose()
        {
            Text.Use(Conventions.Default.Lang);
            foreach (var folder in _folders)
                try { Directory.Delete(folder, true); } catch (Exception) { }
        }

        private string Folder()
        {
            string folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "railgroups-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            _folders.Add(folder);
            return folder;
        }

        private const string Groups = @"""groups"": [
            { ""id"": ""broadcaster"", ""title"": ""Broadcaster"", ""pages"": [ ""baOnline"", ""baOffline"" ] },
            { ""id"": ""friends"", ""title"": ""Friends"", ""pages"": [ ""sfOnline"" ] } ]";

        /// <summary>Five pages: General and Probe in no group, Online and Offline for the broadcaster, one for friends.
        /// The pages name Friends online between the broadcaster's two.</summary>
        private static string SchemaJson(string groups = Groups, string more = null, string root = null) =>
            @"{ ""host"": ""demo"", ""version"": ""v1"", ""settingsFile"": ""x"", ""settings"": [
                { ""key"": ""g1"", ""default"": 1, ""page"": ""general"", ""label"": ""General one"" },
                { ""key"": ""b1"", ""default"": 1, ""page"": ""baOnline"", ""label"": ""Online text"" },
                { ""key"": ""b2"", ""default"": 1, ""page"": ""baOffline"", ""label"": ""Offline text"" },
                { ""key"": ""f1"", ""default"": 1, ""page"": ""sfOnline"", ""label"": ""Friend zebra"" } ],"
            + (root == null ? "" : root + ",") + @"
              ""window"": { ""title"": ""Demo"", ""pages"": [ { ""id"": ""general"", ""title"": ""General"" }, { ""id"": ""baOnline"", ""title"": ""Online"" },
                { ""id"": ""sfOnline"", ""title"": ""Friends online"" }, { ""id"": ""baOffline"", ""title"": ""Offline"" },
                { ""id"": ""probe"", ""title"": ""Probe"", ""custom"": true } ]"
            + (groups == null ? "" : ", " + groups) + (more == null ? "" : ", " + more) + " } }";

        private static WindowSpec Resolved(string schemaJson, string folder, bool customizing = false)
        {
            var schema = Schema.Parse(schemaJson);
            var spec = Entry.SpecFor(schema, Path.Combine(folder, "settings.json"));
            spec.Customizing = customizing;
            Entry.Prepare(spec, schema);
            return spec;
        }

        /// <summary>The rail as the resolve leaves it: each page's id, with its group in front of it where it has one.</summary>
        private static string[] RailOf(WindowSpec spec) => spec.Pages.Select(p => p.Group == null ? p.Id : p.Group + "/" + p.Id).ToArray();

        private static JObject LayoutOn(string folder)
        {
            string file = Path.Combine(folder, LayoutFile.FileName);
            return File.Exists(file) ? JObject.Parse(File.ReadAllText(file)) : new JObject();
        }

        private static void WriteLayout(string folder, string json) => File.WriteAllText(Path.Combine(folder, LayoutFile.FileName), json);

        // ---- The rail's order ------------------------------------------------------------------------------

        [Fact]
        public void AGroupSitsWhereItsFirstPageDoesWithItsPagesInItsOwnOrder()
        {
            // The group names Offline before Online; the pages name Online first, then the friends' page.
            string groups = Groups.Replace(@"""baOnline"", ""baOffline""", @"""baOffline"", ""baOnline""");
            var spec = Resolved(SchemaJson(groups), Folder());
            Assert.Equal(new[] { "general", "broadcaster/baOffline", "broadcaster/baOnline", "friends/sfOnline", "probe", "about" }, RailOf(spec));
            Assert.Equal(new[] { "broadcaster", "friends" }, spec.RailGroups.Select(g => g.Id));
            Assert.Equal(new[] { "Broadcaster", "Friends" }, spec.RailGroups.Select(g => g.Title));
            Assert.Equal(new[] { "baOffline", "baOnline" }, spec.RailGroups[0].Pages);
            Assert.DoesNotContain(spec.Problems, p => p.Contains("group"));
            // The pages at the foot are in no group and stay last.
            Assert.True(spec.Pages.Last().Bottom);
            Assert.Null(spec.Pages.Last().Group);
        }

        [Fact]
        public void TheRailOfAWindowShowsHeadingsAndCountsCtrlNumbersInItsOrder()
        {
            using (var w = new Opened(SchemaJson(), Folder()))
            {
                Assert.Equal(new[] { "General", "[Broadcaster]", "Online", "Offline", "[Friends]", "Friends online", "Probe" }, w.On(() => w.Rail()));
                // Ctrl+number follows the rail as shown, not the order the pages are declared in (Friends online is third there).
                Assert.Equal(w.On(() => Text.T("rail.shortcut", "Offline", 3)), w.On(() => w.Item("Offline").ToolTip as string));
                Assert.Equal(w.On(() => Text.T("rail.shortcut", "Friends online", 4)), w.On(() => w.Item("Friends online").ToolTip as string));
                Assert.True(w.On(() => w.OnPage("g1")));
            }
        }

        // ---- Folding -----------------------------------------------------------------------------------------

        [Fact]
        public void FoldingIsKeptInTheLayoutFileAndAcrossAReopen()
        {
            string folder = Folder();
            using (var w = new Opened(SchemaJson(), folder))
            {
                Assert.True(w.On(() => w.Shown(w.Item("Friends online"))));
                w.On(() => w.Press(w.Heading("friends")));
                Assert.False(w.On(() => w.Shown(w.Item("Friends online"))));
                Assert.True(w.On(() => Rail.IsFolded(w.Heading("friends"))));
                Assert.True(w.On(() => w.Shown(w.Item("Offline"))));          // the other group is not touched
            }
            Assert.True((bool)LayoutOn(folder)["groups"]["friends"]["folded"]);
            Assert.Null(LayoutOn(folder)["groups"]["broadcaster"]);

            using (var w = new Opened(SchemaJson(), folder))
            {
                Assert.False(w.On(() => w.Shown(w.Item("Friends online"))));
                // A rebuild in place (the theme, a reset) keeps it too.
                w.On(() => w.Context.Reopen(null));
                Assert.False(w.On(() => w.Shown(w.Item("Friends online"))));
                // By keyboard: Enter on the heading unfolds, and the file forgets the group.
                w.On(() => w.Key(w.Heading("friends"), Key.Enter));
                Assert.True(w.On(() => w.Shown(w.Item("Friends online"))));
                Assert.Null(LayoutOn(folder)["groups"]);
                // Left folds, Right unfolds.
                w.On(() => w.Key(w.Heading("friends"), Key.Left));
                Assert.False(w.On(() => w.Shown(w.Item("Friends online"))));
                w.On(() => w.Key(w.Heading("friends"), Key.Right));
                Assert.True(w.On(() => w.Shown(w.Item("Friends online"))));
            }
        }

        [Fact]
        public void GoingToAPageUnfoldsItsGroupAndTheGroupOfThePageShownStaysOpen()
        {
            string folder = Folder();
            WriteLayout(folder, @"{ ""groups"": { ""friends"": { ""folded"": true } } }");
            using (var w = new Opened(SchemaJson(), folder))
            {
                Assert.True(w.On(() => Rail.IsFolded(w.Heading("friends"))));
                // The reopen on a page of the folded group (a rebuild that asks for it): the group unfolds, for good.
                w.On(() => w.Context.Reopen("sfOnline"));
                Assert.True(w.On(() => w.OnPage("f1")));
                Assert.True(w.On(() => w.Shown(w.Item("Friends online"))));
                Assert.Null(LayoutOn(folder)["groups"]);
                // A click on the heading of the group that holds the page shown folds nothing.
                w.On(() => w.Press(w.Heading("friends")));
                Assert.True(w.On(() => w.Shown(w.Item("Friends online"))));
                Assert.Null(LayoutOn(folder)["groups"]);
                Assert.Equal(w.On(() => Text.T("rail.group.shown", "Friends")), w.On(() => w.Heading("friends").ToolTip as string));
                // From another page it folds.
                w.On(() => w.Press(w.Item("General")));
                w.On(() => w.Press(w.Heading("friends")));
                Assert.False(w.On(() => w.Shown(w.Item("Friends online"))));
            }
        }

        [Fact]
        public void AWindowOpenedAnewStartsOnTheFirstPageTheRailShows()
        {
            // General moved into the broadcaster's group by the user, and that group folded: the first page shown is the friends'.
            string folder = Folder();
            WriteLayout(folder, @"{ ""pages"": { ""general"": { ""group"": ""broadcaster"" } }, ""groups"": { ""broadcaster"": { ""folded"": true } } }");
            using (var w = new Opened(SchemaJson(), folder))
            {
                Assert.Equal(new[] { "[Broadcaster]", "Online", "Offline", "General", "[Friends]", "Friends online", "Probe" }, w.On(() => w.Rail()));
                Assert.True(w.On(() => w.OnPage("f1")));
                Assert.True(w.On(() => Rail.IsFolded(w.Heading("broadcaster"))));
            }
            Assert.True((bool)LayoutOn(folder)["groups"]["broadcaster"]["folded"]);
        }

        [Fact]
        public void ATourStopUnfoldsAGroupForItsTime()
        {
            string folder = Folder();
            WriteLayout(folder, @"{ ""tourSeen"": true, ""groups"": { ""friends"": { ""folded"": true } } }");
            string tour = @"""tour"": [ { ""target"": ""page:sfOnline"", ""text"": ""The friends."" } ]";
            using (var w = new Opened(SchemaJson(more: tour), folder))
            {
                Assert.False(w.On(() => w.Shown(w.Item("Friends online"))));
                w.On(() => w.Click(Text.T("tip.tour")));
                Assert.True(w.On(() => w.OnPage("f1")));
                Assert.True(w.On(() => w.Shown(w.Item("Friends online"))));
                Assert.True((bool)LayoutOn(folder)["groups"]["friends"]["folded"]);   // the user's folding is not rewritten
                w.On(() => w.Key(w.Window, Key.Escape, Keyboard.PreviewKeyDownEvent));
                Assert.True(w.On(() => w.OnPage("g1")));
                Assert.False(w.On(() => w.Shown(w.Item("Friends online"))));
            }
        }

        // ---- The search --------------------------------------------------------------------------------------

        [Fact]
        public void TheSearchShowsAFoldedGroupThatHoldsAMatchAndGivesTheFoldingBack()
        {
            string folder = Folder();
            using (var w = new Opened(SchemaJson(), folder))
            {
                w.On(() => w.Press(w.Heading("broadcaster")));
                Assert.False(w.On(() => w.Shown(w.Item("Offline"))));

                var box = w.On(() => w.OpenSearch());
                w.On(() => box.Text = "offline text");
                Assert.True(w.On(() => w.Shown(w.Item("Offline"))), "the folded group with the match stays folded");
                Assert.True((bool)LayoutOn(folder)["groups"]["broadcaster"]["folded"]);
                // A match elsewhere leaves it folded; an empty box gives the folding back.
                w.On(() => box.Text = "zebra");
                Assert.False(w.On(() => w.Shown(w.Item("Offline"))));
                w.On(() => box.Text = "offline text");
                w.On(() => box.Text = "");
                Assert.False(w.On(() => w.Shown(w.Item("Offline"))));
                // So does closing the bar.
                w.On(() => box.Text = "offline text");
                w.On(() => w.Key(box, Key.Escape, Keyboard.PreviewKeyDownEvent));
                Assert.False(w.On(() => w.Shown(w.Item("Offline"))));
                Assert.True((bool)LayoutOn(folder)["groups"]["broadcaster"]["folded"]);

                // Going to the result unfolds its group for good.
                box = w.On(() => w.OpenSearch());
                w.On(() => box.Text = "offline text");
                w.On(() => w.Press(w.Results().First()));
                Assert.True(w.On(() => w.OnPage("b2")));
                Assert.True(w.On(() => w.Shown(w.Item("Offline"))));
                Assert.Null(LayoutOn(folder)["groups"]);
            }
        }

        // ---- The customise mode ------------------------------------------------------------------------------

        private static void Sta(Action body)
        {
            Exception failure = null;
            var thread = new Thread(() => { try { body(); } catch (Exception ex) { failure = ex; } });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }

        private static MenuItem MenuEntry(ItemsControl menu, string header) => menu.Items.OfType<MenuItem>().First(i => (string)i.Header == header);
        private static void Click(MenuItem item) => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        private static void DropPage(PageContext ctx, UIElement on, string pageId)
        {
            var data = new DataObject("UIFramework.Page", pageId);
            data.SetData("UIFramework.Layout", ctx.Layout.Path);
            var drop = (DragEventArgs)typeof(DragEventArgs).GetConstructors(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .First(c => c.GetParameters().Length == 5)
                .Invoke(new object[] { data, DragDropKeyStates.None, DragDropEffects.Move, on, new Point() });
            drop.RoutedEvent = DragDrop.DropEvent;
            on.RaiseEvent(drop);
        }

        [Fact]
        public void TheCustomiseModeMovesAPageBetweenGroupsAndOutAndKeepsItInTheLayoutFile() => Sta(() =>
        {
            string folder = Folder(), settingsPath = Path.Combine(folder, "settings.json");
            var schema = Schema.Parse(SchemaJson());
            var spec = Resolved(SchemaJson(), folder, customizing: true);
            var ctx = Entry.ContextFor(spec, new JObject(), new WindowParts(), new QuietLog(), null);
            List<PageSpec> Top() => spec.Pages.Where(p => !p.Bottom).ToList();
            Border Page(string id)
            {
                var item = new Border();
                Customize.AttachRail(ctx, spec.Pages.First(p => p.Id == id), item, Top);
                return item;
            }
            Border Heading(string id)
            {
                var heading = new Rail().Heading(id, id, () => { });
                Customize.AttachGroup(ctx, spec.RailGroups.First(g => g.Id == id), heading, Top);
                return heading;
            }
            string Kept(string id) => LayoutFile.Load(settingsPath).Page(id)?.Group;
            Assert.Equal(new[] { "general", "broadcaster/baOnline", "broadcaster/baOffline", "friends/sfOnline", "probe", "about" }, RailOf(spec));

            // By the menu into another group: behind its last page.
            var into = MenuEntry(Page("sfOnline").ContextMenu, Text.T("cust.moveToGroup"));
            Assert.False(MenuEntry(into, "Friends").IsEnabled);           // where it is
            Click(MenuEntry(into, "Broadcaster"));
            Entry.Prepare(spec, schema);
            Assert.Equal(new[] { "general", "broadcaster/baOnline", "broadcaster/baOffline", "broadcaster/sfOnline", "probe", "about" }, RailOf(spec));
            Assert.Equal("broadcaster", Kept("sfOnline"));
            Assert.Empty(spec.RailGroups.First(g => g.Id == "friends").Pages);

            // By the menu out of every group: to just below the group it was in.
            Click(MenuEntry(MenuEntry(Page("baOnline").ContextMenu, Text.T("cust.moveToGroup")), Text.T("cust.noGroup")));
            Entry.Prepare(spec, schema);
            Assert.Equal(new[] { "general", "broadcaster/baOffline", "broadcaster/sfOnline", "baOnline", "probe", "about" }, RailOf(spec));
            Assert.Equal("", Kept("baOnline"));

            // Dropped on a group's heading (here one left without a page): into that group.
            DropPage(ctx, Heading("friends"), "baOnline");
            Entry.Prepare(spec, schema);
            Assert.Equal(new[] { "general", "broadcaster/baOffline", "broadcaster/sfOnline", "friends/baOnline", "probe", "about" }, RailOf(spec));
            Assert.Equal("friends", Kept("baOnline"));

            // Dropped on a page of a group: beside that page, in its group. Back where the window declares it, the
            // layout file says nothing about its group any more.
            DropPage(ctx, Page("baOnline"), "sfOnline");
            Entry.Prepare(spec, schema);
            Assert.Equal(new[] { "general", "broadcaster/baOffline", "friends/baOnline", "friends/sfOnline", "probe", "about" }, RailOf(spec));
            Assert.Null(Kept("sfOnline"));

            // Dropped on a page in no group: out of its own.
            DropPage(ctx, Page("general"), "baOffline");
            Entry.Prepare(spec, schema);
            Assert.Equal(new[] { "baOffline", "general", "friends/baOnline", "friends/sfOnline", "probe", "about" }, RailOf(spec));
            Assert.Equal("", Kept("baOffline"));

            // Move up from a group's first page takes it out of the group, above its heading; once more goes on as before.
            Click(MenuEntry(Page("baOnline").ContextMenu, Text.T("cust.moveUp")));
            Entry.Prepare(spec, schema);
            Assert.Equal(new[] { "baOffline", "general", "baOnline", "friends/sfOnline", "probe", "about" }, RailOf(spec));
            // Move down from above a group goes into it, as its first page.
            Click(MenuEntry(Page("baOnline").ContextMenu, Text.T("cust.moveDown")));
            Entry.Prepare(spec, schema);
            Assert.Equal(new[] { "baOffline", "general", "friends/baOnline", "friends/sfOnline", "probe", "about" }, RailOf(spec));

            // A page at the foot is in no group and is offered none.
            Assert.DoesNotContain(Page("about").ContextMenu.Items.OfType<MenuItem>(), i => (string)i.Header == Text.T("cust.moveToGroup"));

            // Reset this page gives the page back to the group that declares it.
            Click(MenuEntry(Page("baOffline").ContextMenu, Text.T("cust.resetPage")));
            Entry.Prepare(spec, schema);
            Assert.Equal("broadcaster", spec.Pages.First(p => p.Id == "baOffline").Group);
        });

        [Fact]
        public void TheCustomiseModeRenamesAGroupsHeading() => Sta(() =>
        {
            string folder = Folder();
            var schema = Schema.Parse(SchemaJson());
            var spec = Resolved(SchemaJson(), folder, customizing: true);
            spec.Layout.SetGroup("broadcaster", "title", "Me");
            spec.Layout.SetGroup("broadcaster", "folded", true);
            spec.Layout.Save();
            Entry.Prepare(spec, schema);
            Assert.Equal("Me", spec.RailGroups.First(g => g.Id == "broadcaster").Title);
            Assert.Equal(1, spec.Layout.ChangeCount);

            // The heading's menu gives the declared name back; whether the group is folded is not a change of the layout's.
            var ctx = Entry.ContextFor(spec, new JObject(), new WindowParts(), new QuietLog(), null);
            var heading = new Rail().Heading("broadcaster", "Me", () => { });
            Customize.AttachGroup(ctx, spec.RailGroups.First(g => g.Id == "broadcaster"), heading, () => spec.Pages.Where(p => !p.Bottom).ToList());
            Assert.Equal(Text.T("cust.group.tip", "Me"), heading.ToolTip as string);
            Click(MenuEntry(heading.ContextMenu, Text.T("cust.resetGroup")));
            Entry.Prepare(spec, schema);
            Assert.Equal("Broadcaster", spec.RailGroups.First(g => g.Id == "broadcaster").Title);
            Assert.Equal(0, spec.Layout.ChangeCount);
            Assert.True(spec.Layout.GroupFolded("broadcaster"));

            // Reset layout takes a name back and leaves the folding.
            spec.Layout.SetGroup("friends", "title", "Mates");
            spec.Layout.ResetLayout();
            Assert.Null(spec.Layout.Group("friends"));
            Assert.True(spec.Layout.GroupFolded("broadcaster"));
        });

        [Fact]
        public void TheCustomiseModeShowsEveryGroupUnfoldedAndAnEmptyOneToo()
        {
            string folder = Folder();
            WriteLayout(folder, @"{ ""pages"": { ""sfOnline"": { ""group"": """" } }, ""groups"": { ""broadcaster"": { ""folded"": true } } }");
            using (var w = new Opened(SchemaJson(), folder, customizing: true))
            {
                Assert.Equal(new[] { "General", "[Broadcaster]", "Online", "Offline", "Friends online", "Probe", "[Friends]" }, w.On(() => w.Rail()));
                Assert.True(w.On(() => w.Shown(w.Item("Offline"))));
                // A click on a heading folds nothing while the pages are being arranged.
                w.On(() => w.Press(w.Heading("friends")));
                Assert.True((bool)LayoutOn(folder)["groups"]["broadcaster"]["folded"]);
                Assert.Null(LayoutOn(folder)["groups"]["friends"]);
            }
        }

        // ---- A locked window ---------------------------------------------------------------------------------

        [Fact]
        public void ALockedWindowAppliesNoMoveAndNoNameButKeepsTheFolding()
        {
            string folder = Folder();
            WriteLayout(folder, @"{ ""pages"": { ""sfOnline"": { ""group"": ""broadcaster"" }, ""baOnline"": { ""group"": """" } },
                ""groups"": { ""broadcaster"": { ""title"": ""Me"" }, ""friends"": { ""folded"": true } } }");
            string locked = SchemaJson(more: @"""locked"": true");
            var spec = Resolved(locked, folder);
            Assert.Equal(new[] { "general", "broadcaster/baOnline", "broadcaster/baOffline", "friends/sfOnline", "probe", "about" }, RailOf(spec));
            Assert.Equal("Broadcaster", spec.RailGroups[0].Title);

            using (var w = new Opened(locked, folder))
            {
                Assert.Equal(new[] { "General", "[Broadcaster]", "Online", "Offline", "[Friends]", "Friends online", "Probe" }, w.On(() => w.Rail()));
                Assert.False(w.On(() => w.Shown(w.Item("Friends online"))));      // folded, as the user left it
                w.On(() => w.Press(w.Heading("friends")));
                Assert.True(w.On(() => w.Shown(w.Item("Friends online"))));
                Assert.Null(LayoutOn(folder)["groups"]["friends"]);
                w.On(() => w.Press(w.Heading("broadcaster")));
                Assert.True((bool)LayoutOn(folder)["groups"]["broadcaster"]["folded"]);
                Assert.Equal("Me", (string)LayoutOn(folder)["groups"]["broadcaster"]["title"]);   // kept for the day the lock goes
            }
        }

        // ---- The heading's text ------------------------------------------------------------------------------

        [Fact]
        public void AGroupsTitleIsTranslatedThroughTheProjectsTextsLikeASectionName()
        {
            string json = SchemaJson(root: @"""lang"": { ""de"": { ""texts"": { ""Broadcaster"": ""Sender"" } } }");
            string folder = Folder();
            Assert.Equal("Broadcaster", Resolved(json, folder).RailGroups[0].Title);

            WriteLayout(folder, @"{ ""language"": ""de"" }");
            Assert.Equal("Sender", Resolved(json, folder).RailGroups[0].Title);
            // The user's own name for the heading has the last word, in every language.
            WriteLayout(folder, @"{ ""language"": ""de"", ""groups"": { ""broadcaster"": { ""title"": ""Ich"" } } }");
            Assert.Equal("Ich", Resolved(json, folder).RailGroups[0].Title);
        }

        // ---- What a declaration gets wrong -------------------------------------------------------------------

        [Fact]
        public void WhatCannotWorkIsNamedAmongTheProblems()
        {
            string groups = @"""groups"": [
                { ""title"": ""Nameless"", ""pages"": [ ""baOnline"" ] },
                { ""id"": ""a"", ""pages"": [ ""general"" ] },
                { ""id"": ""a"", ""title"": ""Again"", ""pages"": [ ""baOffline"" ] },
                { ""id"": ""b"", ""title"": ""B"", ""pages"": [ ""nowhere"", ""general"", ""about"", ""sfOnline"" ] } ]";
            var spec = Resolved(SchemaJson(groups), Folder());
            Assert.Contains(spec.Problems, p => p.StartsWith("A group of pages ('Nameless') has no id; it is left out"));
            Assert.Contains(spec.Problems, p => p.StartsWith("The group 'a' has no title; its heading shows '"));
            Assert.Contains(spec.Problems, p => p.StartsWith("Two groups of pages have the id 'a'; the second is left out"));
            Assert.Contains("The group 'b' names the page 'nowhere', which the window does not have.", spec.Problems);
            Assert.Contains("The groups 'a' and 'b' both name the page 'general'; it stays in 'a'.", spec.Problems);
            Assert.Contains(spec.Problems, p => p.StartsWith("The group 'b' names the page 'about', which sits at the foot of the rail; it stays there"));
            Assert.Equal(6, spec.Problems.Count(p => p.Contains("roup")));

            // What can work still does: the first 'a' with its page, 'b' with the page that is its own.
            Assert.Equal(new[] { "a/general", "baOnline", "b/sfOnline", "baOffline", "probe", "about" }, RailOf(spec));
            Assert.Equal(new[] { "a", "b" }, spec.RailGroups.Select(g => g.Id));
            Assert.Equal(Conventions.Humanize("a"), spec.RailGroups[0].Title);
        }

        [Fact]
        public void AGroupTheLayoutFileNamesThatTheWindowDoesNotHaveIsNotApplied()
        {
            string folder = Folder();
            WriteLayout(folder, @"{ ""pages"": { ""sfOnline"": { ""group"": ""gone"" }, ""general"": { ""group"": 7 } } }");
            var spec = Resolved(SchemaJson(), folder);
            Assert.Equal(new[] { "general", "broadcaster/baOnline", "broadcaster/baOffline", "friends/sfOnline", "probe", "about" }, RailOf(spec));
        }

        // ---- A window without groups -------------------------------------------------------------------------

        [Fact]
        public void AWindowThatDeclaresNoGroupIsAsBefore()
        {
            string folder = Folder();
            // What the user's layout says about groups means nothing without one.
            WriteLayout(folder, @"{ ""pages"": { ""sfOnline"": { ""group"": ""broadcaster"" } }, ""groups"": { ""broadcaster"": { ""folded"": true, ""title"": ""Me"" } } }");
            var spec = Resolved(SchemaJson(groups: null), folder);
            Assert.Equal(new[] { "general", "baOnline", "sfOnline", "baOffline", "probe", "about" }, RailOf(spec));
            Assert.Empty(spec.RailGroups);
            Assert.Empty(spec.Problems);

            using (var w = new Opened(SchemaJson(groups: null), folder))
            {
                Assert.Equal(new[] { "General", "Online", "Friends online", "Offline", "Probe" }, w.On(() => w.Rail()));
                Assert.True(w.On(() => w.OnPage("g1")));
                Assert.Equal(w.On(() => Text.T("rail.shortcut", "Friends online", 3)), w.On(() => w.Item("Friends online").ToolTip as string));
                // The pages' items sit in the rail itself, as they always did.
                Assert.Same(w.On(() => w.Item("General").Parent), w.On(() => w.Item("Offline").Parent));
            }
            Sta(() =>
            {
                var rail = new Rail();
                var first = rail.Item("One", "•", () => { });
                var second = rail.Item("Two", "•", () => { });
                Assert.Equal(new[] { first, second }, rail.Stops);
                Assert.Equal(rail.Items, rail.Stops);
            });
        }

        // ---- The window, opened as a project opens it --------------------------------------------------------

        /// <summary>Shell.Run on a thread of its own, as the settings action does. The custom page "probe" hands the test
        /// each build's context, and with it the window's thread and controls.</summary>
        private sealed class Opened : NoHooks, IDisposable
        {
            public readonly string SettingsPath;
            private readonly Thread _runner;
            private readonly ManualResetEventSlim _built = new ManualResetEventSlim();
            private volatile PageContext _context;
            private volatile FrameworkElement _probe;

            public Opened(string schemaJson, string folder, bool customizing = false)
            {
                SettingsPath = Path.Combine(folder, "settings.json");
                var schema = UIFramework.Schema.Parse(schemaJson);
                var spec = Entry.SpecFor(schema, SettingsPath);
                spec.Customizing = customizing;
                _runner = new Thread(() => { try { Entry.Show(spec, schema, new QuietLog(), this); } catch (Exception) { } }) { IsBackground = true };
                _runner.Start();
                if (!_built.Wait(15000))
                    throw new TimeoutException("The window was not built.");
                Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            }

            public PageContext Context => _context;
            public Dispatcher Dispatcher => _probe.Dispatcher;
            public Window Window
            {
                get
                {
                    DependencyObject node = _probe;
                    while (node != null && !(node is Window))
                        node = LogicalTreeHelper.GetParent(node) ?? (node is System.Windows.Media.Visual ? System.Windows.Media.VisualTreeHelper.GetParent(node) : null);
                    return (Window)node;
                }
            }

            public override FrameworkElement BuildPage(string pageId, PageContext context)
            {
                _context = context;
                _probe = new TextBlock { Text = "probe" };
                _built.Set();
                return _probe;
            }

            public T On<T>(Func<T> body) => Dispatcher.Invoke(body);
            public void On(Action body) => Dispatcher.Invoke(body);

            private static IEnumerable<object> All(object node)
            {
                yield return node;
                if (node is DependencyObject d)
                    foreach (var child in LogicalTreeHelper.GetChildren(d))
                        foreach (var below in All(child))
                            yield return below;
            }

            private static bool IsHeading(Border b) => b.Tag is string tag && tag.StartsWith(UIFramework.Rail.GroupTag, StringComparison.Ordinal);

            /// <summary>A page's entry in the rail, by the page's title.</summary>
            public Border Item(string title) => All(Window).OfType<Border>().First(b => b.Name.StartsWith("Rail", StringComparison.Ordinal) && !IsHeading(b) && AutomationProperties.GetName(b) == title);

            /// <summary>A group's heading in the rail, by the group's id.</summary>
            public Border Heading(string id) => All(Window).OfType<Border>().First(b => b.Tag as string == UIFramework.Rail.GroupTag + id);

            /// <summary>The rail above its foot from top to bottom: the pages' titles, a heading as "[Title]".</summary>
            public string[] Rail()
            {
                var read = new List<string>();
                void Read(object node)
                {
                    if (node is Border b && IsHeading(b)) read.Add("[" + AutomationProperties.GetName(b) + "]");
                    else if (node is Border item && item.Name.StartsWith("Rail", StringComparison.Ordinal)) read.Add(AutomationProperties.GetName(item));
                    else if (node is Border frame) Read(frame.Child);
                    else if (node is Panel panel) foreach (var child in panel.Children) Read(child);
                }
                // The top of the rail is the first panel docked in it; the foot is docked to the bottom.
                var railBorder = All(Window).OfType<ScrollViewer>().First(v => v.Content is DockPanel dock && dock.Children.Count == 2 && dock.Children[0] is StackPanel && dock.Children[1] is StackPanel);
                Read(((DockPanel)railBorder.Content).Children[1]);
                return read.ToArray();
            }

            /// <summary>Whether an element shows: neither it nor anything it sits in is hidden.</summary>
            public bool Shown(FrameworkElement element) => Shell.ShownOn(element, Window);

            /// <summary>Whether the page with this setting's field is the one in front.</summary>
            public bool OnPage(string key) => Shown(_context.Parts.Elements[key]);

            public void Press(UIElement element) =>
                element.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });

            public void Key(UIElement element, Key key, RoutedEvent how = null) =>
                element.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(Window), 0, key) { RoutedEvent = how ?? Keyboard.KeyDownEvent });

            /// <summary>A button by its tooltip, clicked as the mouse would.</summary>
            public void Click(string tip)
            {
                var button = All(Window).OfType<Button>().First(b => b.ToolTip as string == tip);
                button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
            }

            /// <summary>Opens the search bar by its button and hands back its box.</summary>
            public TextBox OpenSearch()
            {
                var button = All(Window).OfType<Button>().First(b => b.Name == "SearchButton");
                button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
                return All(Window).OfType<TextBox>().First(b => b.Name == SearchBar.BoxName);
            }

            /// <summary>The rows the search lists right now.</summary>
            public List<Border> Results() => All(Window).OfType<Border>().Where(b => b.Tag as string == SearchBar.RowTag).ToList();

            public void Dispose()
            {
                try
                {
                    Shell.Close(SettingsPath);
                    var watch = Stopwatch.StartNew();
                    while (_runner.IsAlive && watch.ElapsedMilliseconds < 15000)
                        Thread.Sleep(50);
                }
                finally
                {
                    Text.Use(Conventions.Default.Lang);
                }
            }
        }

        private sealed class QuietLog : IHostLog
        {
            public void Info(string message) { }
            public void Warn(string message) { }
            public void Error(string message) { }
        }
    }
}
