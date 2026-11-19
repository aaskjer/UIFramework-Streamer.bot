using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>
    /// Renders a page from its settings: section headings where the section changes, numbers three to a row,
    /// switches two to a row, settings naming the same <c>row</c> side by side, everything else on its own line.
    /// Each kind maps to one of the <see cref="Fields"/>.
    /// </summary>
    public static class SchemaPage
    {
        // A time of day is as short as a number and lines up with them.
        private static readonly HashSet<string> NumberKinds = new HashSet<string> { "int", "long", "double", "percent", "duration", "time" };

        public static UIElement Build(PageContext ctx, List<Setting> settings, string pageId = null)
        {
            var page = Ui.PageStack();
            var scroll = Ui.Scroll(page);
            if (ctx.Customizing)
            {
                FillCustomizing(ctx, page, settings);
                Customize.AcceptDrops(ctx, scroll, page, pageId ?? (settings.Count > 0 ? settings[0].Page : null));
            }
            else
                Fill(ctx, page, settings, true);
            return scroll;
        }

        /// <summary>The customise mode: one field per line, each in a draggable frame with its menu; hidden ones
        /// dimmed so they can be shown again.</summary>
        private static void FillCustomizing(PageContext ctx, StackPanel page, List<Setting> settings)
        {
            // The page as the window shows it, rows and all, every field in a draggable frame; hidden ones dimmed.
            string pageId = settings.Count > 0 ? settings[0].Page : "";
            Fill(ctx, page, settings, true, (s, element) => Customize.Wrap(ctx, s, element, pageId));
            if (settings.Count == 0)
                page.Children.Add(Ui.Note(Text.T("cust.emptyPage")));
        }

        /// <summary>Fields the spec put on a standard page, above that page's own content. A page with nothing of its
        /// own (no messages, no help) shows the fields alone instead of going with them.</summary>
        public static UIElement Prepend(PageContext ctx, List<Setting> settings, UIElement inner)
        {
            if (settings.Count == 0)
                return inner;
            if (inner == null)
                return Build(ctx, settings);
            var top = Ui.PageStack();
            top.Margin = new Thickness(16, 2, 16, 0);
            if (ctx.Customizing)
                FillCustomizing(ctx, top, settings);
            else
                Fill(ctx, top, settings, true);
            var dock = new DockPanel();
            DockPanel.SetDock(top, Dock.Top);
            dock.Children.Add(top);
            dock.Children.Add(inner);
            return dock;
        }

        private static void ShowAnswerBox(StackPanel panel)
        {
            if (panel.Parent is Border box && Equals(box.Tag, AnswerBox))
                box.Visibility = panel.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>Marks the box an action's answer goes into, so a row can put it under itself.</summary>
        private const string AnswerBox = "action-answer";

        private static void Fill(PageContext ctx, StackPanel page, List<Setting> settings, bool firstHeading,
            Func<Setting, FrameworkElement, FrameworkElement> wrap = null)
        {
            string section = null;
            var row = new List<FrameworkElement>();
            string rowKind = null;
            int rowMax = 1;
            FrameworkElement heading = null;
            var sectionFields = new List<FrameworkElement>();

            void Flush()
            {
                if (row.Count == 0) return;
                // A number on its own keeps a third of the width, as it would beside two others.
                if (row.Count == 1 && rowKind != "bool" && rowKind != "number")
                    page.Children.Add(row[0]);
                else
                {
                    var cells = row.ToList();
                    while (cells.Count < rowMax && rowKind == "number")
                        cells.Add(new TextBlock());
                    // In a named row a button takes the room it needs, and an answer box goes under the whole row.
                    var answers = new List<FrameworkElement>();
                    var lines = new List<FrameworkElement>();
                    var weights = cells.Select(c => 1.0).ToArray();
                    if (rowKind != null && rowKind.StartsWith("row ", StringComparison.Ordinal))
                        for (int i = 0; i < cells.Count; i++)
                        {
                            // A field with its help line under it is looked at through that wrapper.
                            var cell = Core(cells[i]);
                            if (cell is StackPanel stack && stack.Children.Count == 2 && stack.Children[0] is Button
                                && stack.Children[1] is Border box && Equals(box.Tag, AnswerBox))
                            {
                                stack.Children.Remove(box);
                                // Under the row, the answer still follows its action: hidden while the action is. The
                                // action's conditions hide the cell, its help-line wrapper when it has one.
                                var holder = new Border { Child = box };
                                holder.SetBinding(UIElement.VisibilityProperty, new System.Windows.Data.Binding("Visibility") { Source = cells[i] });
                                answers.Add(holder);
                            }
                            if (cell is Button || (cell is StackPanel only && only.Children.Count == 1 && only.Children[0] is Button))
                            {
                                weights[i] = 0;
                                // A button's column is only as wide as the button, so its help line goes under the row,
                                // where it can wrap; it hides with the button.
                                if (!ReferenceEquals(cell, cells[i]) && cells[i] is StackPanel wrapper && wrapper.Children.Count == 2 && wrapper.Children[1] is FrameworkElement line)
                                {
                                    wrapper.Children.Remove(line);
                                    var under = new Border { Child = line };
                                    under.SetBinding(UIElement.VisibilityProperty, new System.Windows.Data.Binding("Visibility") { Source = wrapper });
                                    lines.Add(under);
                                }
                            }
                        }
                    page.Children.Add(Ui.Row(weights, cells.ToArray()));
                    foreach (var line in lines)
                        page.Children.Add(line);
                    foreach (var box in answers)
                        page.Children.Add(box);
                }
                row.Clear();
                rowKind = null;
            }

            // A heading whose every field is hidden (showWhen) hides with them.
            void WatchSection()
            {
                if (heading == null || sectionFields.Count == 0) return;
                VisibilityWatch.ShowWhileAnyVisible(heading, sectionFields.ToList());
            }

            foreach (var setting in settings.Where(s => wrap != null || !s.Hidden).OrderBy(s => s.Order))
            {
                if (setting.Section != null && setting.Section != section)
                {
                    Flush();
                    WatchSection();
                    section = setting.Section;
                    var title = Ui.Section(section, firstHeading && page.Children.Count == 0);
                    heading = title;
                    sectionFields.Clear();
                    page.Children.Add(heading);
                    // A heading that reads otherwise while a condition holds (.Section(name, title:, titleWhen:)).
                    var other = ctx.Spec?.SectionTitles.FirstOrDefault(t => t.Name == setting.SectionId && (t.Page == null || t.Page == setting.Page));
                    if (other != null && wrap == null)
                    {
                        string plain = section, worded = ctx.Spec.Conventions?.Lang?.Text(other.Title) ?? other.Title;
                        ctx.AfterBuild.Add(() => Requirement.Wire(ctx.Fields, ctx.Parts, other.When, on => title.Text = on ? worded : plain));
                    }
                }

                var element = Render(ctx, setting);
                if (element == null)
                    continue;
                ctx.Parts.Elements[setting.Key] = element;
                if (wrap != null)
                    element = wrap(setting, element);
                sectionFields.Add(element);

                string kind = NumberKinds.Contains(setting.Kind) ? "number" : setting.Kind == "bool" ? "bool" : "single";
                int max = kind == "number" ? 3 : kind == "bool" ? 2 : 1;
                if (!string.IsNullOrEmpty(setting.Row))
                {
                    // Settings naming the same row, one after the other, share it whatever their kind.
                    kind = "row " + setting.Row;
                    max = int.MaxValue;
                    InRow(element);
                }
                if (kind == "single" || (rowKind != null && rowKind != kind) || row.Count >= rowMax)
                    Flush();
                rowKind = kind;
                rowMax = max;
                row.Add(element);
                if (kind == "single")
                    Flush();
            }
            Flush();
            WatchSection();
        }

        /// <summary>A button or a switch beside labelled fields sits on their baseline. With a help line under it, the
        /// line hangs below the field, so the field is lined up from the top like a switch.</summary>
        private static void InRow(FrameworkElement element)
        {
            var core = Core(element);
            if (core is Button button)
            {
                if (ReferenceEquals(core, element))
                    button.VerticalAlignment = VerticalAlignment.Bottom;
                else
                    Ui.Held(button);
            }
            else if (core is CheckBox toggle)
                Ui.Held(toggle);
        }

        /// <summary>The field inside a help-line wrapper, or the element itself.</summary>
        private static FrameworkElement Core(FrameworkElement element) =>
            element is StackPanel wrapper && Equals(wrapper.Tag, HelpWrapTag) && wrapper.Children.Count == 2 && wrapper.Children[0] is FrameworkElement field
                ? field : element;

        private const string HelpWrapTag = "uiframework.helpWrap";

        /// <summary>A number's label with its unit behind it, "Every (min)". Whole numbers showed none, though the
        /// timer's intervals rely on it; a label that names the unit already (a game's own "Every (min)") stays.</summary>
        public static string WithUnit(string label, string unit)
        {
            if (string.IsNullOrWhiteSpace(unit)) return label;
            string tail = "(" + unit.Trim() + ")";
            return (label ?? "").TrimEnd().EndsWith(tail, StringComparison.OrdinalIgnoreCase) ? label : label + " " + tail;
        }

        public static FrameworkElement Render(PageContext ctx, Setting s)
        {
            var f = ctx.Fields;
            string label = s.Label ?? Conventions.Humanize(s.Key);
            // A switch carries "Twitch only" as a pill; any other field says it in its label (a reward's id, a button).
            // It was read for switches alone and did nothing elsewhere, without a word.
            if (s.TwitchOnly && s.Kind != "bool" && s.Kind != "note")
                label += " (" + Text.T("field.twitchOnly") + ")";
            string help = s.Help ?? "";
            // The user may have the help as a line under each field instead of in its tooltip (the customise bar). A
            // note and a list show their help as text anyway.
            bool helpLine = ctx.Layout?.HelpLines == true && !string.IsNullOrWhiteSpace(help) && s.Kind != "note" && s.Kind != "list";
            string tip = helpLine ? null : help;
            FrameworkElement element;
            if (s.ReadOnly)
            {
                // Shown, not edited; its help, its conditions and its section's go with it like any field's. Several
                // choices show by their labels, a time as it is written ("09:05"), or as it is stored when it is none.
                string shown = s.Kind == "multi" ? Fields.MultiLabels(s.Options, f.Token(s.Key))
                    : s.Kind == "time" && FieldBinding.TryTime(f.String(s.Key), out string time) ? time
                    : s.Kind == "country" ? CountryShown(ctx, s, f.String(s.Key)) : f.String(s.Key);
                var value = new TextBlock { Text = shown, FontSize = 13, Foreground = Theme.LabelFg, Margin = new Thickness(0, 4, 0, 4), TextWrapping = s.Kind == "multi" ? TextWrapping.Wrap : TextWrapping.NoWrap };
                element = s.Locked ? LockedField(ctx, s, label, value) : Ui.Labeled(label, value);
                if (!string.IsNullOrEmpty(tip)) element.ToolTip = tip;
            }
            else switch (s.Kind)
            {
                case "bool":
                {
                    var toggle = f.Toggle(label, s.Key, tip);
                    if (s.TwitchOnly) Fields.TwitchOnly(toggle);
                    element = toggle;
                    break;
                }
                case "int":
                    element = s.Options != null && s.Options.Count > 0
                        ? f.IntCombo(label, s.Key, s.Options.Select(o => (int.TryParse(o.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0, o.Label)).ToArray(), tip)
                        : f.IntField(WithUnit(label, s.Unit), s.Key, tip, s.UnlimitedAtZero);
                    SetRange(ctx, s);
                    break;
                case "long":
                    element = f.LongField(WithUnit(label, s.Unit), s.Key, tip, s.UnlimitedAtZero);
                    SetRange(ctx, s);
                    break;
                case "duration":
                    element = f.DurationField(label, s.Key, tip, s.Unit, s.UnlimitedAtZero);
                    SetRange(ctx, s);
                    break;
                case "double":
                    if (s.Options != null && s.Options.Count > 0)
                        element = f.DoubleCombo(label, s.Key, s.Options.Select(o => (double.TryParse(o.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : 0.0, o.Label)).ToArray(), tip);
                    else
                    {
                        element = f.DoubleField(WithUnit(label, s.Unit), s.Key, tip);
                        SetRange(ctx, s);
                    }
                    break;
                case "percent":
                    element = f.PercentField(label, s.Key, s.Factor ?? 1.0, tip, s.Min ?? 0, s.Max ?? 100);
                    break;
                case "options":
                    if (s.OptionsFrom != null)
                    {
                        element = OptionsFromField(ctx, s, label, tip);
                        break;
                    }
                    var options = (s.Options ?? new List<SettingOption>()).Select(o => (o.Value, o.Label)).ToArray();
                    element = options.Length == 0 ? (s.Masked ? f.MaskedField(label, s.Key, tip, s.Hint) : f.TextField(label, s.Key, tip, s.Hint)) : f.StringComboField(label, s.Key, options, tip);
                    break;
                case "country":
                    element = f.StringComboField(label, s.Key, CountryOptions(ctx, s, f.String(s.Key)), tip);
                    break;
                case "namelist":
                    element = Stack(Ui.SmallLabel(label, 6), f.NameList(label, s.Key, Text.T("field.namelist.hint"), tip));
                    break;
                case "grouplist":
                    element = Stack(Ui.SmallLabel(label, 6), f.NameList(label, s.Key, Text.T("field.grouplist.hint"), tip, names => StreamerBotGroups(ctx, names)));
                    break;
                case "multi":
                    // Nothing to tick without options: a text field, as an options field without options is one. A field
                    // named like the heading it stands under shows no caption of its own: the heading says it already.
                    element = Fields.MultiChoices(s.Options).Count == 0
                        ? f.TextField(label, s.Key, tip)
                        : string.Equals((label ?? "").Trim(), (s.Section ?? "").Trim(), StringComparison.OrdinalIgnoreCase)
                            ? f.MultiField(label, s.Key, s.Options, tip)
                            : Stack(Ui.SmallLabel(label, 6), f.MultiField(label, s.Key, s.Options, tip));
                    break;
                case "time":
                    // A unit is said behind the label, as a number's: "Announce at (UTC)".
                    element = f.TimeField(WithUnit(label, s.Unit), s.Key, tip);
                    break;
                case "colour":
                case "color":
                    element = f.ColourField(label, s.Key, tip);
                    break;
                case "path":
                    element = PathField(ctx, s, label, tip);
                    break;
                case "emote":
                    element = EmoteField(ctx, s, label, tip);
                    break;
                case "slider":
                {
                    bool stops = s.Options != null && s.Options.Count > 0;
                    bool whole = s.Default == null || s.Default.Type != JTokenType.Float;
                    element = f.SliderField(label, s.Key, tip, s.Options, s.Min ?? 0, s.Max ?? (stops ? s.Options.Count - 1 : 100), s.Step ?? (whole ? 1 : 0.1), whole, s.Unit);
                    break;
                }
                case "note":
                {
                    var note = Ui.NoteWithLinks(string.IsNullOrEmpty(help) ? (s.Default?.Type == JTokenType.String ? (string)s.Default : "") : help, ctx.OpenUrl);
                    note.Margin = new Thickness(0, 8, 0, 0);
                    element = note;
                    break;
                }
                case "text":
                case "template":
                    var box = f.TemplateBox(label, s.Key);
                    box.ToolTip = tip;
                    element = Ui.Labeled(label, box);
                    break;
                case "action":
                    element = ActionButton(ctx, s, label, tip);
                    break;
                case "table":
                    element = Table(ctx, s, label, tip);
                    break;
                case "list":
                {
                    // A list the project fills as well (rowKey, fixedRows, filter) writes back what the user did not
                    // change as it was loaded. Its filter reads the fields once they are all built.
                    var columns = Translated(ctx, s.Columns);
                    var filter = ListFilter.Parse(s.Filter);
                    var editor = new ListEditor(s.Key, label, columns, f.Token(s.Key) as JArray, ctx.Defaults?[s.Key] as JArray, help, ctx.Hooks,
                        s.FixedRows, ListMerge.Applies(s) || filter != null);
                    ctx.Parts.Lists[s.Key] = editor;
                    if (filter != null)
                        ctx.AfterBuild.Add(() => filter.Wire(ctx.Fields, ctx.Parts, columns, editor));
                    editor.Margin = new Thickness(0, 8, 0, 0);
                    element = string.IsNullOrEmpty(help) ? (FrameworkElement)editor : Stack(Ui.Note(help), editor);
                    break;
                }
                default:
                    element = s.Masked ? f.MaskedField(label, s.Key, tip, s.Hint) : f.TextField(label, s.Key, tip, s.Hint);
                    break;
            }
            if (s.Transient)
                FieldBinding.MarkTransient(element);
            if ((s.Ask || s.Live) && s.Kind != "action" && !s.ReadOnly)
            {
                // A field that asks the host whenever it changes; `set` in the answer fills in other fields (a preset slider).
                // Each question has a number, as a button's: an answer that comes after a later question was asked
                // (it waited behind another request) does not put its values over the later one's.
                var source = element;
                int asked = 0;
                ctx.AfterBuild.Add(() => FieldBinding.HookChangeTracking(source, () =>
                {
                    int mine = ++asked;
                    Ask(ctx, s, label, null, () => mine == asked);
                }));
            }

            // The help line goes with the field: greyed and hidden with it.
            if (helpLine)
                element = WithHelpLine(ctx, element, help);

            // Greyed out while `requires` does not hold; hidden while `showWhen` or the section's condition does not.
            // All of the hiding conditions must hold to show the field. A `requires` with "hide:" in front is no
            // longer read (the red i says so): such a field shows, and is not greyed by a key that is none.
            var target = element;
            string grey = null;
            var show = new List<string>();
            if (!string.IsNullOrWhiteSpace(s.Requires) && !s.Requires.Trim().StartsWith("hide:", StringComparison.OrdinalIgnoreCase))
                grey = s.Requires.Trim();
            if (!string.IsNullOrWhiteSpace(s.ShowWhen)) show.Add(s.ShowWhen.Trim());
            if (!string.IsNullOrWhiteSpace(s.SectionShowWhen)) show.Add(s.SectionShowWhen.Trim());
            if (ctx.Customizing)
                return element;
            // A condition on a multi field asks whether a value is ticked, also while that field is hidden and its
            // saved text is read: every multi setting's choices are known to the conditions, shown or not.
            if (grey != null || show.Count > 0)
                foreach (var multi in ctx.Spec?.ResolvedSettings?.Where(x => x.Kind == "multi") ?? Enumerable.Empty<Setting>())
                    if (!ctx.Parts.Choices.ContainsKey(multi.Key) && Fields.MultiChoices(multi.Options) is var choices && choices.Count > 0)
                        ctx.Parts.Choices[multi.Key] = choices.Select(o => o.Value).ToArray();
            if (grey != null)
                ctx.AfterBuild.Add(() => Requirement.Wire(ctx.Fields, ctx.Parts, grey, on => Grey(target, !on)));
            if (show.Count > 0)
            {
                string all = string.Join(" && ", show);
                ctx.AfterBuild.Add(() => Requirement.Wire(ctx.Fields, ctx.Parts, all, on => target.Visibility = on ? Visibility.Visible : Visibility.Collapsed));
            }
            return element;
        }

        /// <summary>What a country field offers, by the names of the window's language: every country, or the ones its
        /// <c>options</c> name. An entry for "no country" comes first where the default is none, where an option with
        /// an empty value names it, or where the file holds none (so opening the window picks no country by itself).
        /// An option's own label stands in place of the country's name.</summary>
        public static (string Value, string Label)[] CountryOptions(PageContext ctx, Setting s, string stored)
        {
            string language = ctx?.Spec?.Conventions?.Lang?.Code;
            var declared = (s.Options ?? new List<SettingOption>()).Where(o => o != null).ToList();
            var none = declared.FirstOrDefault(o => string.IsNullOrWhiteSpace(o.Value));
            var named = declared.Where(o => !string.IsNullOrWhiteSpace(o.Value)).ToList();
            var entries = new List<(string Value, string Label)>();
            bool noDefault = s.Default == null || s.Default.Type == JTokenType.Null || FieldBinding.Invariant(s.Default).Trim().Length == 0;
            if (none != null || noDefault || string.IsNullOrWhiteSpace(stored))
                entries.Add(("", none != null && !string.IsNullOrWhiteSpace(none.Label) ? none.Label : Text.T("field.country.none")));
            foreach (var country in Countries.Listed(language, named.Count > 0 ? named.Select(o => o.Value).Distinct(StringComparer.OrdinalIgnoreCase) : null))
            {
                var own = named.FirstOrDefault(o => string.Equals(o.Value.Trim(), country.Code, StringComparison.OrdinalIgnoreCase));
                bool relabelled = own != null && !string.IsNullOrWhiteSpace(own.Label) && !string.Equals(own.Label.Trim(), own.Value.Trim(), StringComparison.OrdinalIgnoreCase);
                entries.Add((country.Code, relabelled ? own.Label : country.Label));
            }
            return entries.ToArray();
        }

        /// <summary>A country as a field that is only shown says it: the name with the code, or what is stored.</summary>
        private static string CountryShown(PageContext ctx, Setting s, string stored)
        {
            var entry = CountryOptions(ctx, s, stored).FirstOrDefault(o => string.Equals(o.Value, (stored ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
            return entry.Label ?? stored ?? "";
        }

        /// <summary>A field with its help as a small line underneath, links in it clickable.</summary>
        private static FrameworkElement WithHelpLine(PageContext ctx, FrameworkElement field, string help) => HelpUnder(ctx, field, help);

        /// <summary>Whether the user has the help as lines under the fields (the customise bar's switch) instead of in
        /// the tooltips. The pages the framework draws itself ask this for their own fields.</summary>
        public static bool HelpLines(PageContext ctx) => ctx.Layout?.HelpLines == true;

        /// <summary>A field that is locked: its label with a pill, and under it why. One a Set Argument of the
        /// project's action sets shows no value: what the file holds is not what counts while the argument is there.
        /// One the project locks itself shows its value, a secret as dots. One kept in a global the window does not
        /// read (<see cref="Setting.GlobalOff"/>) shows none either: the window does not know it.</summary>
        private static FrameworkElement LockedField(PageContext ctx, Setting s, string label, TextBlock value)
        {
            bool argument = s.Argument != null;
            var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            head.Children.Add(new TextBlock { Text = label, FontSize = 11, Foreground = Theme.SubFg, VerticalAlignment = VerticalAlignment.Center });
            head.Children.Add(Ui.Pill(Text.T(argument ? "field.setArgument" : "field.locked"), Theme.AccentSoft, Theme.AccentText));
            var panel = new StackPanel();
            panel.Children.Add(head);
            if (!argument && !s.GlobalOff)
            {
                if (s.Masked && value.Text.Length > 0) value.Text = new string('•', 8);
                panel.Children.Add(value);
            }
            string why = argument ? Text.T("field.setArgument.why", s.Argument) : s.LockReason;
            if (!string.IsNullOrWhiteSpace(why))
            {
                // Its own line, not the field's help line: it shows whether the help sits under the fields or in tooltips.
                var line = Ui.NoteWithLinks(why, ctx.OpenUrl);
                line.FontSize = 11;
                line.Margin = new Thickness(0, 3, 0, 6);
                panel.Children.Add(line);
            }
            System.Windows.Automation.AutomationProperties.SetName(panel, label + " (" + Text.T(argument ? "field.setArgument" : "field.locked") + ")");
            return panel;
        }

        /// <summary>A help text as the small line that goes under a field.</summary>
        public static TextBlock HelpLine(PageContext ctx, string help)
        {
            var line = Ui.NoteWithLinks(help, ctx.OpenUrl);
            line.FontSize = 11;
            line.Margin = new Thickness(0, 3, 0, 6);   // apart from the next field's label
            line.Tag = HelpLineTag;
            return line;
        }

        /// <summary>A field with its help as a line underneath.</summary>
        public static FrameworkElement HelpUnder(PageContext ctx, FrameworkElement field, string help)
        {
            var line = HelpLine(ctx, help);
            var wrapper = new StackPanel { Tag = HelpWrapTag };
            wrapper.Children.Add(field);
            wrapper.Children.Add(line);
            return wrapper;
        }

        /// <summary>The Tag of a help line under a field, for tests and the tour.</summary>
        public const string HelpLineTag = "uiframework.helpLine";

        /// <summary>Greys a field out, or back in. Disabled alone looks the same as enabled on some kinds (a list, a
        /// slider, a label above a box), so the whole field is also dimmed.</summary>
        public static void Grey(UIElement field, bool grey)
        {
            field.IsEnabled = !grey;
            field.Opacity = grey ? 0.5 : 1.0;
        }

        /// <summary>The columns with their labels in the window's language: a declared label the conventions or the
        /// game's own translations know is replaced, the rest stays as declared.</summary>
        private static List<SettingColumn> Translated(PageContext ctx, List<SettingColumn> columns)
        {
            var lang = ctx.Spec.Conventions?.Lang;
            if (columns == null || lang == null || !lang.Translates)
                return columns;
            return columns.Select(c => new SettingColumn { Key = c.Key, Kind = c.Kind, Label = lang.Text(c.Label), Platform = c.Platform, ReadOnly = c.ReadOnly }).ToList();
        }

        private static void SetRange(PageContext ctx, Setting s)
        {
            if (!s.Min.HasValue && !s.Max.HasValue) return;
            if (ctx.Parts.Boxes.TryGetValue(s.Key, out var box) && box.Tag is FieldTag tag)
            {
                if (s.Min.HasValue) tag.Min = s.Min.Value;
                if (s.Max.HasValue) tag.Max = s.Max.Value;
            }
        }

        private static StackPanel Stack(params UIElement[] children)
        {
            var panel = new StackPanel();
            foreach (var child in children)
                panel.Children.Add(child);
            return panel;
        }

        /// <summary>A text box for one emote with its picture beside it (see <see cref="EmotePictures"/>).</summary>
        private static FrameworkElement EmoteField(PageContext ctx, Setting s, string label, string help)
        {
            var box = ctx.Fields.SettingBox(label, s.Key, help);
            // YouTube's own emojis (":name:") have their pictures in the table inside the DLL, as in a list's column.
            string platform = (s.Platform ?? "twitch").ToLowerInvariant();
            var image = EmotePictures.Picture(platform, box.Text, ctx.Hooks);
            image.Margin = new Thickness(0, 2, 6, 0);
            box.LostFocus += (sender, e) => EmotePictures.Show(image, platform, box.Text, ctx.Hooks);
            var dock = new DockPanel();
            DockPanel.SetDock(image, Dock.Left);
            dock.Children.Add(image);
            dock.Children.Add(box);
            return Ui.Labeled(label, dock);
        }

        // Streamer.bot's groups, asked of the settings action once per window (one hooks object each, as the emote
        // sources are), when a grouplist field first gets the cursor; every grouplist field of the window shares the
        // answer, also after the window rebuilt its pages. The question goes off the window's thread (Patient): a
        // Streamer.bot that takes its time leaves the box to type in meanwhile.
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IHostHooks, GroupsAnswer> GroupsAsked =
            new System.Runtime.CompilerServices.ConditionalWeakTable<IHostHooks, GroupsAnswer>();

        private sealed class GroupsAnswer
        {
            public List<string> Names;
            public readonly List<Action<List<string>>> Waiting = new List<Action<List<string>>>();
        }

        private static void StreamerBotGroups(PageContext ctx, Action<List<string>> then)
        {
            var hooks = ctx.Hooks;
            var answer = GroupsAsked.GetValue(hooks, h => new GroupsAnswer());
            if (answer.Names != null) { then(answer.Names); return; }
            answer.Waiting.Add(then);
            if (answer.Waiting.Count > 1) return;   // asked already: the answer goes to every field waiting for it
            Patient.Ask(() => hooks.Groups(), (json, error) =>
            {
                answer.Names = GroupNames(error == null ? json : null);
                var waiting = answer.Waiting.ToList();
                answer.Waiting.Clear();
                foreach (var field in waiting)
                    field(answer.Names);
            });
        }

        /// <summary>The names in the settings action's answer to Groups, a JSON array of strings: sorted, without empty
        /// and repeated ones. None for no answer (a settings action before stub 0.3.2, a preview) or one that is no
        /// such array.</summary>
        public static List<string> GroupNames(string json)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(json) && JsonText.Parse(json) is JArray array)
                    return array.Where(t => t.Type == JTokenType.String).Select(t => ((string)t).Trim()).Where(n => n.Length > 0)
                        .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList();
            }
            catch (Exception) { }   // no JSON: nothing to suggest
            return new List<string>();
        }

        /// <summary>How long after the last change of the field a dropdown's <c>optionsFrom</c> names the project is asked
        /// for the options again, in milliseconds. The tests shorten it.</summary>
        public static int OptionsFromDelayMs = 350;

        /// <summary>A dropdown whose options come from the project (<c>optionsFrom</c>). "groups": Streamer.bot's groups,
        /// from the settings action. Otherwise the project is asked (<see cref="IHostHooks.Options"/>) with the values
        /// as they stand in the window, once every page is built, and again a moment after the last change of the
        /// field the text names; an empty text, the dropdown's own key or one no field shows asks that once only.</summary>
        /// <remarks>Asked off the window's thread (<see cref="Patient"/>). Each question has a number: an answer that
        /// comes after a later question was asked is dropped. A change that leaves the window as it was when the last
        /// question was asked (there and back within the moment) asks nothing. No options in the answer, no answer and
        /// one that cannot be read leave the declared options; the latter two leave a line in the log.</remarks>
        private static FrameworkElement OptionsFromField(PageContext ctx, Setting s, string label, string help)
        {
            var declared = (s.Options ?? new List<SettingOption>()).Select(o => (o.Value, o.Label)).ToList();
            var element = ctx.Fields.OfferedComboField(label, s.Key, declared, help, out var offer);
            string from = s.OptionsFrom.Trim();
            if (from == "groups")
            {
                ctx.AfterBuild.Add(() => StreamerBotGroups(ctx, names => offer(names.Select(n => (n, n)).ToList())));
                return element;
            }

            int asked = 0;
            string askedWith = null;
            void Unread(string why) => ctx.Log?.Warn(ctx.Spec?.LogPrefix + " The options of '" + s.Key + "' could not be read: " + why);
            void Ask()
            {
                JObject values;
                string state;
                try
                {
                    // A copy: the question is put together on another thread while the window goes on changing its own.
                    values = (JObject)(ctx.LiveSettings != null ? ctx.LiveSettings() : ctx.Settings).DeepClone();
                    state = values.ToString(Newtonsoft.Json.Formatting.None);
                }
                catch (Exception ex) { Unread(ex.Message); return; }
                if (state == askedWith) return;
                askedWith = state;
                int mine = ++asked;
                Patient.Ask(() => ctx.Hooks.Options(s.Key, values), (json, error) =>
                {
                    if (mine != asked) return;
                    List<SettingOption> options = null;
                    string failed = error != null ? error.Message : Patient.IsNoAnswer(json) ? Patient.Why(json) : null;
                    if (failed == null && (options = OfferedOptions(json)) == null)
                        failed = json.Length > 200 ? json.Substring(0, 200) + "…" : json;
                    if (failed != null)
                    {
                        Unread(failed.Length > 0 ? failed : "no answer");
                        askedWith = null;   // the same values may be asked with again
                    }
                    offer(options?.Select(o => (o.Value, o.Label)).ToList());
                });
            }

            ctx.AfterBuild.Add(() =>
            {
                if (from.Length > 0 && from != s.Key)
                {
                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(1, OptionsFromDelayMs)) };
                    timer.Tick += (sender, e) => { timer.Stop(); Ask(); };
                    OnChange(ctx.Parts, from, () => { timer.Stop(); timer.Start(); });
                }
                Ask();
            });
            return element;
        }

        /// <summary>Calls <paramref name="changed"/> whenever the field of this key changes; never for a key no field
        /// of the window holds.</summary>
        private static void OnChange(WindowParts parts, string key, Action changed)
        {
            if (parts.Switches.TryGetValue(key, out var toggle))
            {
                toggle.Checked += (sender, e) => changed();
                toggle.Unchecked += (sender, e) => changed();
            }
            else if (parts.Combos.TryGetValue(key, out var combo))
                combo.SelectionChanged += (sender, e) => changed();
            else if (parts.Boxes.TryGetValue(key, out var box))
                box.TextChanged += (sender, e) => changed();
            else if (parts.Sliders.TryGetValue(key, out var slider))
                slider.ValueChanged += (sender, e) => changed();
            else if (parts.Lists.TryGetValue(key, out var list))
                list.Changed += changed;
        }

        /// <summary>The options in the project's answer for a dropdown with <c>optionsFrom</c>: a JSON array of
        /// "value", "value|Label" or <c>{ "value": ..., "label": ... }</c>; a number is its own value, anything else
        /// in the array is left out. None for an empty answer; null for one that is no such array.</summary>
        public static List<SettingOption> OfferedOptions(string json)
        {
            var options = new List<SettingOption>();
            if (string.IsNullOrWhiteSpace(json))
                return options;
            JToken answer;
            try { answer = JsonText.Parse(json); }
            catch (Exception) { return null; }
            if (answer.Type == JTokenType.Null) return options;
            if (!(answer is JArray array)) return null;
            foreach (var entry in array)
            {
                if (entry is JObject o)
                {
                    if (!(o["value"] is JValue value) || value.Type == JTokenType.Null) continue;
                    string text = FieldBinding.Invariant(value);
                    options.Add(new SettingOption { Value = text, Label = o["label"] is JValue shown && shown.Type != JTokenType.Null ? FieldBinding.Invariant(shown) : text });
                }
                else if (entry.Type == JTokenType.String || entry.Type == JTokenType.Integer || entry.Type == JTokenType.Float)
                    options.Add(SettingOption.Parse(entry));
            }
            return options;
        }

        private static FrameworkElement PathField(PageContext ctx, Setting s, string label, string help)
        {
            var box = ctx.Fields.SettingBox(label, s.Key, (string.IsNullOrEmpty(help) ? "" : help + "\n") + Text.T("field.path.tip"));
            var open = Ui.IconButton(Icons.Folder, Icons.Font, Text.T("field.path.open"));
            open.Margin = new Thickness(6, 0, 0, 0);
            open.Click += (sender, e) =>
            {
                // A value that names a file opens the folder the file is in: the button made a folder of the file's
                // name ("overlay\state.json"), or failed on a file that was there.
                string folder = PathPicker.FolderToOpen(box.Text, AppDomain.CurrentDomain.BaseDirectory);
                if (folder != null)
                    Web.OpenFolder(folder, ctx.Log, ctx.Spec.LogPrefix);
            };
            // Windows' own dialog: for a file when the value or the default names one, else for a folder. What is chosen
            // under the Streamer.bot folder is written relative to it.
            var browse = Ui.IconButton(PathPicker.BrowseGlyph, Icons.Font, Text.T("field.path.browse"));
            browse.Margin = new Thickness(6, 0, 0, 0);
            browse.Click += (sender, e) =>
            {
                string baseFolder = AppDomain.CurrentDomain.BaseDirectory;
                var plan = PathPicker.For(box.Text, s.Default?.Type == JTokenType.String ? (string)s.Default : null, baseFolder);
                string chosen;
                try { chosen = PathPicker.Choose(plan, Window.GetWindow(box), Text.T(plan.File ? "field.path.pickFile" : "field.path.pickFolder", label), Text.T("field.path.allFiles")); }
                catch (Exception ex)
                {
                    ctx.Log?.Warn(ctx.Spec.LogPrefix + " The dialog for '" + s.Key + "' could not open: " + ex.Message);
                    return;
                }
                if (chosen != null)
                    box.Text = PathPicker.Stored(chosen);
            };
            var dock = new DockPanel();
            DockPanel.SetDock(open, Dock.Right);
            DockPanel.SetDock(browse, Dock.Right);
            dock.Children.Add(open);
            dock.Children.Add(browse);
            dock.Children.Add(box);
            return Ui.Labeled(label, dock);
        }

        /// <summary>The shortest <c>every</c> of an auto action, in seconds. Each question runs the project's action in
        /// Streamer.bot, one after the other with everything else the window asks; a shorter one is taken as this.</summary>
        public const int ShortestEvery = 5;

        /// <summary>How long a second of <c>every</c> lasts, in milliseconds. The tests shorten it.</summary>
        public static int EverySecondMs = 1000;

        /// <summary>The time between two questions of an auto action with <c>every</c>: not below
        /// <see cref="ShortestEvery"/>, and no longer than a timer runs (some 24 days).</summary>
        public static TimeSpan EveryInterval(int seconds) =>
            TimeSpan.FromMilliseconds(Math.Min((long)Math.Max(seconds, ShortestEvery) * Math.Max(1, EverySecondMs), int.MaxValue));

        /// <summary>An action: a button that asks the host. With <c>live</c> the host gets the values as they stand
        /// in the window; with <c>panel</c> its answer goes into a panel under the button; with <c>auto</c> there is
        /// no button, the host is asked when the page is shown and after every change, a moment after the last, and
        /// with <c>every</c> again every so many seconds while the panel shows. With <c>saveFirst</c> the button
        /// saves the window's changes before it asks.</summary>
        private static FrameworkElement ActionButton(PageContext ctx, Setting s, string label, string help)
        {
            StackPanel panel = null;
            if (s.Panel || s.Auto)
                panel = new StackPanel();

            // Each question has a number: an answer that comes after a later question was asked is not shown over it.
            // How many are still out, and whether the window changed since the last one was asked, is what keeps the
            // interval and a return to the page from asking the same thing twice.
            int asked = 0, underWay = 0;
            bool changed = false;
            Button button = null;
            void Run()
            {
                int mine = ++asked;
                changed = false;
                underWay++;
                Ask(ctx, s, label, panel, () => mine == asked, button, () => underWay--);
            }

            if (s.Auto)
            {
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
                timer.Tick += (sender, e) => { timer.Stop(); Run(); };
                ctx.OnAnyChange(() => { changed = true; timer.Stop(); timer.Start(); });
                var box = new Border
                {
                    Background = new SolidColorBrush(Theme.Surface),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 8, 10, 8),
                    Margin = new Thickness(0, 8, 0, 0),
                    ToolTip = string.IsNullOrEmpty(help) ? null : help,
                    Child = panel
                };
                // With `every` it is asked again every so many seconds, while the panel shows: its page is the one in
                // front and the window is open. WPF says when that ends (another page, a rebuild, the window closed)
                // and when it holds again; a hidden panel has no timer running. A question still out is not asked a
                // second time, and one a change is about to ask is left to that. Not in the customise mode, where
                // nothing is to be followed.
                System.Windows.Threading.DispatcherTimer again = null;
                if (s.Every.HasValue && !ctx.Customizing)
                {
                    again = new System.Windows.Threading.DispatcherTimer { Interval = EveryInterval(s.Every.Value) };
                    again.Tick += (sender, e) => { if (underWay == 0 && !changed) Run(); };
                    box.IsVisibleChanged += (sender, e) => { again.Stop(); if (box.IsVisible) again.Start(); };
                }
                ctx.Parts.OnShow(s.Page ?? "", () =>
                {
                    // A question still out that asked with what the window shows now is the one to wait for.
                    if (underWay == 0 || changed) { timer.Stop(); Run(); }
                    // The interval counts from here.
                    if (again != null) { again.Stop(); if (box.IsVisible) again.Start(); }
                });
                return Stack(Ui.SmallLabel(label, 0), box);
            }

            button = Ui.TextButton(label, false);
            button.Margin = new Thickness(0, 8, 0, 0);
            button.HorizontalAlignment = HorizontalAlignment.Left;
            button.ToolTip = help;
            button.Click += (sender, e) =>
            {
                if (s.Confirm != null &&
                    MessageBox.Show(s.Confirm, label, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    return;
                // saveFirst: the window's changes are saved as its Save button saves them, the check and its question
                // included. Without that save the project is not asked: it would answer for what the file still holds.
                if (s.SaveFirst && !ctx.SaveChanges())
                    return;
                Run();
            };
            if (panel == null)
                return button;
            // The answer box shows once there is an answer in it, not as an empty bar before the first click.
            var result = new Border
            {
                Tag = AnswerBox,
                Visibility = Visibility.Collapsed,
                Background = new SolidColorBrush(Theme.Surface),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 8, 0, 0),
                Child = panel
            };
            return Stack(button, result);
        }

        /// <summary>Asks the host about a setting and places the answer: "set" (values for other fields of the window,
        /// applied as if typed, nothing saved) and "status" (a line for the footer) are taken out of a JSON answer; the
        /// rest goes to the panel when there is one, to the footer otherwise.</summary>
        /// <remarks>The game is asked off the window's thread (<see cref="Patient"/>): an answer that takes its time
        /// leaves the window usable, with the button greyed until it is there. No answer at all (the settings action's
        /// marker) is shown as a failure, not as "done".</remarks>
        /// <param name="done">Called once the question is no longer out: answered, failed, or never asked.</param>
        private static void Ask(PageContext ctx, Setting s, string label, StackPanel panel, Func<bool> current = null, Button button = null, Action done = null)
        {
            void Failed(string why)
            {
                if (panel != null) { ResultPanel.Render(panel, Text.T("action.failedWhy", label, why), failed: true); ShowAnswerBox(panel); }
                else ctx.SetStatus(Text.T("action.failed", label), Theme.DangerText, why);
            }
            JObject settings;
            try
            {
                // A field with `ask` asks with what the window shows, as `live` on it did before `ask` had a name; a
                // button does so with `live`. A copy: the question is put together on another thread while the window
                // goes on changing its own.
                bool live = s.Live || (s.Ask && s.Kind != "action");
                settings = live && ctx.LiveSettings != null ? ctx.LiveSettings() : (JObject)ctx.Settings.DeepClone();
            }
            catch (Exception ex) { done?.Invoke(); Failed(ex.Message); return; }
            if (button != null) button.IsEnabled = false;
            Patient.Ask(() => ctx.Hooks.Test(s.Key, settings), (result, error) =>
            {
                done?.Invoke();
                if (button != null) button.IsEnabled = true;
                if (current != null && !current()) return;
                if (error != null) { Failed(error.Message); return; }
                if (Patient.IsNoAnswer(result))
                {
                    string why = Patient.Why(result);
                    if (panel != null) { ResultPanel.Render(panel, Text.T("action.noAnswerWhy", label, why.Length > 0 ? why : "-"), failed: true); ShowAnswerBox(panel); }
                    else ctx.SetStatus(Text.T("action.noAnswer", label), Theme.DangerText, why);
                    return;
                }
                try { Place(ctx, s, label, panel, result); }
                catch (Exception ex) { Failed(ex.Message); }
            },
            waiting: () => { if (panel == null && s.Kind == "action") ctx.SetStatus(Text.T("action.waiting", label), Theme.MutedColor, null); });
        }

        private static void Place(PageContext ctx, Setting s, string label, StackPanel panel, string result)
        {
            {
                JObject structured = ResultPanel.TryParse(result);
                if (structured?["set"] is JObject values)
                    LiveValues.Apply(ctx.Parts, values);
                // "copy": { "to": "from" } puts one field's value into another inside the window, as if typed. The
                // project names the fields and never sees the value: the way to copy a secret, which it is handed
                // only as "" or "*".
                if (structured?["copy"] is JObject copies)
                    LiveValues.Copy(ctx.Fields, ctx.Parts, copies);
                string status = (string)structured?["status"];
                string clipboard = (string)structured?["clipboard"];
                if (clipboard != null)
                {
                    // The window's thread is an STA one, which the clipboard wants; the game's is not.
                    try { Clipboard.SetText(clipboard); }
                    catch (Exception ex) { status = (status ?? "") + Text.T("action.clipboardBusy", ex.Message); }
                }
                if (panel != null)
                {
                    ResultPanel.Render(panel, result ?? "", ctx.Hooks);
                    ShowAnswerBox(panel);
                    // `status` is the footer's line beside a panel too: one answer may carry lines, a bar and a status.
                    if (status != null)
                        ctx.SetStatus(status, Theme.SuccessText, null);
                }
                else if (structured != null)
                {
                    if (status != null || s.Kind == "action")
                        ctx.SetStatus(status ?? Text.T("action.done", label), Theme.SuccessText, null);
                }
                else if (result != null || s.Kind == "action")
                    ctx.SetStatus(result ?? Text.T("action.done", label), Theme.SuccessText, null);
            }
        }

        private static FrameworkElement Table(PageContext ctx, Setting s, string label, string help)
        {
            var columns = Translated(ctx, s.Columns) ?? new List<SettingColumn>();
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserAddRows = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                RowHeaderWidth = 0,
                MaxHeight = 320,
                FontSize = 12,
                Background = new SolidColorBrush(Theme.ContentBg),
                RowBackground = new SolidColorBrush(Theme.ContentBg),
                Foreground = Theme.LabelFg,
                BorderBrush = new SolidColorBrush(Theme.Line),
                BorderThickness = new Thickness(1),
                HorizontalGridLinesBrush = new SolidColorBrush(Theme.Line),
                ColumnHeaderStyle = Ui.HeaderStyle(),
                ToolTip = help
            };
            var cellText = new Style(typeof(TextBlock));
            cellText.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(6, 4, 6, 4)));
            foreach (var column in columns)
                grid.Columns.Add(new DataGridTextColumn
                {
                    Header = column.Label ?? column.Key,
                    Binding = new Binding("[" + column.Key + "]"),
                    Width = DataGridLength.Auto,
                    MinWidth = 50,
                    ElementStyle = cellText
                });

            var empty = Ui.Note(Text.T("table.empty"));
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            var host = new Grid();
            host.Children.Add(grid);
            host.Children.Add(empty);

            // The rows come from the game, asked off the window's thread (Patient): a game that takes its time leaves
            // "Loading" in the table instead of holding the window, and one that does not answer is said to have not.
            int asked = 0;
            void Load()
            {
                int mine = ++asked;
                Patient.Ask(() => ctx.Hooks.ReadTable(s.Key), (json, error) =>
                {
                    if (mine != asked) return;
                    var rows = new List<Dictionary<string, string>>();
                    string failed = null;
                    try
                    {
                        if (error != null) throw error;
                        if (Patient.IsNoAnswer(json)) failed = Patient.Why(json);
                        else if (!string.IsNullOrWhiteSpace(json) && JsonText.Parse(json) is JArray array)
                            foreach (var item in array.OfType<JObject>())
                                rows.Add(item.Properties().ToDictionary(p => p.Name, p => FieldBinding.Invariant(p.Value)));
                    }
                    catch (Exception ex) { failed = ex.Message; }
                    if (failed != null)
                        ctx.Log.Warn(ctx.Spec.LogPrefix + " Table '" + s.Key + "' could not be read: " + failed);
                    grid.ItemsSource = rows;
                    empty.Text = failed != null ? Text.T("table.noAnswer") : Text.T("table.empty");
                    empty.Foreground = failed != null ? new SolidColorBrush(Theme.DangerText) : Theme.MutedFg;
                    empty.ToolTip = string.IsNullOrEmpty(failed) ? null : failed;
                    empty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                },
                waiting: () =>
                {
                    if (grid.Items.Count > 0) return;   // the rows it has stay until the new ones are there
                    empty.Text = Text.T("table.loading");
                    empty.Foreground = Theme.MutedFg;
                    empty.Visibility = Visibility.Visible;
                });
            }

            var reload = Ui.IconButton(Icons.Refresh, Icons.Font, Text.T("table.reload"));
            reload.Click += (sender, e) => Load();
            var head = new DockPanel { Margin = new Thickness(0, 6, 0, 4) };
            DockPanel.SetDock(reload, Dock.Right);
            head.Children.Add(reload);
            head.Children.Add(Ui.SmallLabel(label, 0));
            ctx.Parts.OnShow(s.Page ?? "", Load);
            Load();
            return Stack(head, host);
        }
    }
}
