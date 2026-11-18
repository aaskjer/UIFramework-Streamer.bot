using Xunit;

namespace UIFramework.Tests
{
    /// <summary>What the host block declares for a secret in a global, a dropdown that asks for its options, a status
    /// that asks again, a button that saves first, a list the project writes to, the window's oldest UIFramework and
    /// its groups of pages reaches the schema, and the block answers the request for a dropdown's options.</summary>
    public class HostBlockDeclarationTests
    {
        [Fact]
        public void WhatASettingDeclaresTravelsThroughTheSchema()
        {
            var token = UiSchema.ToJson(UiSchema.S("freeBotToken", "", masked: true, global: "FreeGameCheck_botToken"));
            Assert.Equal("FreeGameCheck_botToken", (string)token["global"]);
            var region = UiSchema.ToJson(UiSchema.S("region", "", kind: "options", optionsFrom: "countryCode"));
            Assert.Equal("countryCode", (string)region["optionsFrom"]);
            var status = UiSchema.ToJson(UiSchema.S("status", null, kind: "action", auto: true, every: 10));
            Assert.Equal(10, (int)status["every"]);
            var check = UiSchema.ToJson(UiSchema.S("checkNow", null, kind: "action", saveFirst: true));
            Assert.True((bool)check["saveFirst"]);
            var list = UiSchema.ToJson(UiSchema.S("holidays", new string[0], kind: "list", columns: new[] { "date", "name" },
                rowKey: "date,name", fixedRows: true, filter: "region= || counties~{region}"));
            Assert.Equal("date,name", (string)list["rowKey"]);
            Assert.True((bool)list["fixedRows"]);
            Assert.Equal("region= || counties~{region}", (string)list["filter"]);

            // Left out, none of them is in the schema: an older window reads what it always read.
            var plain = UiSchema.ToJson(UiSchema.S("sides", 6));
            foreach (string key in new[] { "global", "optionsFrom", "every", "saveFirst", "rowKey", "fixedRows", "filter" })
                Assert.Null(plain[key]);

            // And the DLL reads each as declared, and writes it back.
            Setting Read(JObject item) => Setting.Parse(Setting.Parse(item).ToJson());
            Assert.Equal("FreeGameCheck_botToken", Read(token).Global);
            Assert.Equal("countryCode", Read(region).OptionsFrom);
            Assert.Equal(10, Read(status).Every);
            Assert.True(Read(check).SaveFirst);
            Assert.Equal("date,name", Read(list).RowKey);
            Assert.True(Read(list).FixedRows);
            Assert.Equal("region= || counties~{region}", Read(list).Filter);
            Assert.Null(Read(plain).Every);
        }

        [Fact]
        public void TheWindowsOldestUIFrameworkAndItsGroupsReachTheSpec()
        {
            var window = new UiWindow("Demo", requires: "2.1.0")
                .Group("broadcaster", "Broadcaster", "online", "offline")
                .Group("friends", "Friends");
            var json = window.ToJson();
            Assert.Equal("2.1.0", (string)json["requires"]);
            Assert.Equal(@"[{""id"":""broadcaster"",""title"":""Broadcaster"",""pages"":[""online"",""offline""]},{""id"":""friends"",""title"":""Friends"",""pages"":[]}]",
                json["groups"].ToString(Formatting.None));

            var spec = SpecJson.Parse(json);
            Assert.Equal("2.1.0", spec.Requires);
            Assert.Equal(new[] { "broadcaster", "friends" }, spec.Groups.Select(g => g.Id).ToArray());
            Assert.Equal("Broadcaster", spec.Groups[0].Title);
            Assert.Equal(new[] { "online", "offline" }, spec.Groups[0].Pages.ToArray());

            var bare = new UiWindow("Demo").ToJson();
            Assert.Null(bare["requires"]);
            Assert.Null(bare["groups"]);
            Assert.Null(SpecJson.Parse(bare).Requires);
            Assert.Empty(SpecJson.Parse(bare).Groups);
        }

        [Fact]
        public void TheBlockAnswersTheOptionsOfADropdown()
        {
            var cph = new FakeCph();
            string asked = null, country = null;
            bool Handle(Dictionary<string, object> args, bool withOptions = true) => UiHost.Handle(args, cph, "Demo Game", "demo", "v1.0.0", "Demo/settings.json",
                options: withOptions ? (key, s) => { asked = key; country = s.Raw.Value<string>("countryCode"); return key == "region" ? new[] { "DE-BY|Bayern", "DE-BE" } : null; } : (Func<string, UiSettings, string[]>)null);

            Assert.True(Handle(new Dictionary<string, object> { ["uiframework.request"] = "options", ["uiframework.name"] = "region", ["uiframework.values"] = "{\"countryCode\": \"DE\"}" }));
            Assert.Equal("region", asked);
            Assert.Equal("DE", country);
            Assert.Equal(@"[""DE-BY|Bayern"",""DE-BE""]", cph.Globals[UiHost.AnswerVar("Demo Game")]);

            // A key the project names no options for, and a project without the delegate: no answer text.
            Assert.True(Handle(new Dictionary<string, object> { ["uiframework.request"] = "options", ["uiframework.name"] = "other", ["uiframework.values"] = "{}" }));
            Assert.Equal("", cph.Globals[UiHost.AnswerVar("Demo Game")]);
            Assert.True(Handle(new Dictionary<string, object> { ["uiframework.request"] = "options", ["uiframework.name"] = "region", ["uiframework.values"] = "{}" }, withOptions: false));
            Assert.Equal("", cph.Globals[UiHost.AnswerVar("Demo Game")]);
        }

        [Fact]
        public void AHostWithoutTheNewMethodsHasNoGlobalsAndNamesNoOptions()
        {
            // A settings action from before stub 0.3.3: the window finds neither, and says so rather than failing.
            var old = new ReflectionHost(new OldHost());
            Assert.False(old.HasGlobals);
            Assert.Null(old.GetGlobal("x"));
            Assert.False(old.SetGlobal("x", "y"));
            Assert.Null(old.Options("region", new JObject()));

            var current = new CurrentHost();
            var host = new ReflectionHost(current);
            Assert.True(host.HasGlobals);
            Assert.True(host.SetGlobal("token", "abc"));
            Assert.Equal("abc", host.GetGlobal("token"));
            Assert.Equal(@"[""a""]", host.Options("region", new JObject { ["countryCode"] = "DE" }));
            Assert.Equal(@"region {""countryCode"":""DE""}", current.Asked);
        }

        public sealed class OldHost
        {
            public void Log(string level, string message) { }
        }

        public sealed class CurrentHost
        {
            private readonly Dictionary<string, string> _globals = new Dictionary<string, string>();
            public string Asked;
            public string GetGlobal(string name) => _globals.TryGetValue(name, out var value) ? value : null;
            public void SetGlobal(string name, string value) => _globals[name] = value;
            public string Options(string key, string settingsJson) { Asked = key + " " + settingsJson; return @"[""a""]"; }
        }
    }
}
