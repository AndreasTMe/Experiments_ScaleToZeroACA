namespace InfraRunner.Infra;

// Matches the names in infra/main.bicep and infra/apps.bicep.
internal static class InfraNames
{
    public const string Repository = "scale-to-zero-worker";

    public static string ResourceGroup(string namePrefix, string location) => $"rg-{namePrefix}-{location}";

    public static string Budget(string namePrefix) => $"budget-{namePrefix}";

    public static string BaseDeployment(string namePrefix) => $"{namePrefix}-base";

    public static string AppsDeployment(string namePrefix) => $"{namePrefix}-apps";

    public static string ImageTag(string variant, string imageTag) => $"{variant}-{imageTag}";
}