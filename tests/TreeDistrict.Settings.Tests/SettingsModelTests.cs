using System;
using System.IO;
using Newtonsoft.Json.Linq;
using Xunit;

namespace TreeDistrict.Tests
{
    public class SettingsModelTests
    {
        private static JObject State()
        {
            var state = SettingsModel.Initial(SettingsModel.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "catalog.json"))));
            foreach (var id in new[] { "area-a", "area-b" })
            {
                var record = (JObject)state["draft"].DeepClone();
                record["id"] = id; record["name"] = id; record["seed"] = 1; record["revision"] = 0; record["generationMode"] = "onDemand";
                ((JArray)state["districts"]).Add(record);
            }
            return state;
        }
        [Fact]
        public void EditingOneAreaDoesNotChangeOtherAreasOrLibrary()
        {
            var before = State(); var set = SettingsModel.Target(before, "area-a")["working"].DeepClone(); set["overallDensityPercent"] = 31;
            var after = SettingsModel.Apply(before, new JObject { ["type"] = "editSet", ["districtId"] = "area-a", ["set"] = set });
            Assert.Equal(31, (int)SettingsModel.Target(after, "area-a")["working"]["overallDensityPercent"]);
            Assert.True(JToken.DeepEquals(before["library"], after["library"]));
            Assert.True(JToken.DeepEquals(SettingsModel.Target(before, "area-b"), SettingsModel.Target(after, "area-b")));
            Assert.False(JToken.DeepEquals(before, after));
        }
        [Fact]
        public void FailedSaveAndReplaceIsAtomic()
        {
            var state = State(); string original = state.ToString();
            Assert.Throws<ArgumentException>(() => SettingsModel.Apply(state, new JObject { ["type"] = "saveSet", ["districtId"] = "area-a", ["name"] = "My set", ["newId"] = "user:test", ["afterSaveLibraryId"] = "missing" }));
            Assert.Equal(original, state.ToString());
        }
        [Fact]
        public void LibraryUpdateKeepsOtherAreaBaselineAndSettings()
        {
            var state = State();
            state = SettingsModel.Apply(state, new JObject { ["type"] = "saveSet", ["districtId"] = "area-a", ["name"] = "Custom", ["newId"] = "user:custom" });
            state = SettingsModel.Apply(state, new JObject { ["type"] = "chooseSet", ["districtId"] = "area-b", ["libraryId"] = "user:custom" });
            var before = SettingsModel.Target(state, "area-b").DeepClone();
            var set = SettingsModel.Target(state, "area-a")["working"].DeepClone(); set["overallDensityPercent"] = 17;
            state = SettingsModel.Apply(state, new JObject { ["type"] = "editSet", ["districtId"] = "area-a", ["set"] = set });
            state = SettingsModel.Apply(state, new JObject { ["type"] = "saveSet", ["districtId"] = "area-a", ["name"] = "Custom" });
            Assert.True(JToken.DeepEquals(before, SettingsModel.Target(state, "area-b")));
        }
        [Theory]
        [InlineData("missing")]
        [InlineData("")]
        public void InvalidAreaTargetCannotEditDraft(string id)
        {
            var state = State();
            Assert.Throws<ArgumentException>(() => SettingsModel.Apply(state, new JObject { ["type"] = "newSet", ["districtId"] = id }));
        }
        [Fact]
        public void RejectsInvalidNestedCurveAndDuplicateAssets()
        {
            var set = SettingsModel.Target(State(), null)["working"].DeepClone();
            set["assets"][0]["ages"][0]["slope"]["optimum"] = 91;
            Assert.Throws<ArgumentException>(() => SettingsModel.ValidateSet(set));
            set = SettingsModel.Target(State(), null)["working"].DeepClone();
            ((JArray)set["assets"]).Add(set["assets"][0].DeepClone());
            Assert.Throws<ArgumentException>(() => SettingsModel.ValidateSet(set));
        }
        [Fact]
        public void ClimateDefaultsDoNotModifySetInheritanceOrBaseline()
        {
            var state = State(); var city = state["city"].DeepClone(); city["referenceTemperatureC"] = 12;
            var next = SettingsModel.Apply(state, new JObject { ["type"] = "city", ["city"] = city });
            Assert.True(JToken.DeepEquals(state["districts"], next["districts"]));
            Assert.True(JToken.DeepEquals(state["draft"], next["draft"]));
        }
        [Fact]
        public void BuiltInsCannotBeOverwrittenAndUnknownCommandsAreRejected()
        {
            var state = State();
            Assert.Throws<ArgumentException>(() => SettingsModel.Apply(state, new JObject { ["type"] = "saveSet", ["districtId"] = "area-a", ["name"] = "Overwrite" }));
            Assert.Throws<ArgumentException>(() => SettingsModel.Apply(state, new JObject { ["type"] = "createDistrict", ["districtId"] = "area-a" }));
        }
    }
}
