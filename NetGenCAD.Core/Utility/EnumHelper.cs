using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using static NetGenCAD.Core.Measurements.Selector;

namespace NetGenCAD.Core.Utility
{
    // Store (const) enums from NetGenCAD.Core here to be used as static resources or compiled bindings
    public static class EnumHelper
    {
        [UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "<Pending>")]
        public static IReadOnlyList<BackgroundType> BackgroundTypeValues => [.. Enum.GetValues(typeof(BackgroundType)).Cast<BackgroundType>()];

        [UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "<Pending>")]
        public static IReadOnlyList<UnitSystem> UnitSystemValues => [.. Enum.GetValues(typeof(UnitSystem)).Cast<UnitSystem>()];

        [UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "<Pending>")]
        public static IReadOnlyList<CoreSolidType> CoreSolidTypeValues => [.. Enum.GetValues(typeof(CoreSolidType)).Cast<CoreSolidType>()];

        [UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "<Pending>")]
        public static IReadOnlyList<string> PortableFontValues => [.. Enum.GetValues<PortableFont>().Select(v => v.ToString()).ToList()];

        [UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "<Pending>")]
        public static IReadOnlyList<string> TextLanguageValues => [.. Enum.GetValues<TextLanguage>().Select(v => v.ToString().ToUpper()).ToList()];

        /// <summary>
        /// Simple item for binding an enum value + its Description to UI (e.g. ComboBox).
        /// </summary>
        public class EnumOption
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public object? Value { get; set; }

            public override string ToString() => Description;
        }

        /// <summary>
        /// Builds an ObservableCollection of EnumOption from an enum type,
        /// using the [Description] attribute on each member.
        /// </summary>
        public static class EnumDescriptionHelper
        {
            [UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "<Pending>")]
            public static ObservableCollection<EnumOption> GetOptions<T>() where T : Enum
            {
                var collection = new ObservableCollection<EnumOption>();

                foreach (var value in Enum.GetValues(typeof(T)))
                {
                    var field = typeof(T).GetField(value.ToString()!);
                    var descAttr = field?.GetCustomAttribute<DescriptionAttribute>();

                    collection.Add(new EnumOption
                    {
                        Name = value.ToString()!,
                        Description = descAttr?.Description ?? value.ToString()!,
                        Value = value
                    });
                }

                return collection;
            }
        }

        public static class ViewModelEnums
        {
            public enum ApiAvailability
            {
                Offline,
                Local,
                Online
            }
        }
    }
}
