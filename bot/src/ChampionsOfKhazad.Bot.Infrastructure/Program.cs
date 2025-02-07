using Pulumi;
using Pulumi.AzureNative.App;
using Pulumi.AzureNative.Insights;
using Pulumi.AzureNative.OperationalInsights;
using Pulumi.AzureNative.OperationalInsights.Inputs;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Web;
using Pulumi.AzureNative.Web.Inputs;
using Pulumi.Docker;
using Pulumi.Docker.Inputs;
using AppLogsConfigurationArgs = Pulumi.AzureNative.App.Inputs.AppLogsConfigurationArgs;
using Config = Pulumi.Config;
using ConfigurationArgs = Pulumi.AzureNative.App.Inputs.ConfigurationArgs;
using ContainerApp = Pulumi.AzureNative.App.ContainerApp;
using ContainerAppArgs = Pulumi.AzureNative.App.ContainerAppArgs;
using ContainerArgs = Pulumi.AzureNative.App.Inputs.ContainerArgs;
using ContainerResourcesArgs = Pulumi.AzureNative.App.Inputs.ContainerResourcesArgs;
using EnvironmentVarArgs = Pulumi.AzureNative.App.Inputs.EnvironmentVarArgs;
using LogAnalyticsConfigurationArgs = Pulumi.AzureNative.App.Inputs.LogAnalyticsConfigurationArgs;
using RegistryCredentialsArgs = Pulumi.AzureNative.App.Inputs.RegistryCredentialsArgs;
using ScaleArgs = Pulumi.AzureNative.App.Inputs.ScaleArgs;
using SecretArgs = Pulumi.AzureNative.App.Inputs.SecretArgs;
using TemplateArgs = Pulumi.AzureNative.App.Inputs.TemplateArgs;

return await Pulumi.Deployment.RunAsync(() =>
{
    var config = new Config();
    var providerConfig = new Config("azure-native");

    var resourceGroup = new ResourceGroup(
        "resource-group",
        new ResourceGroupArgs { ResourceGroupName = "cok-memes" },
        new CustomResourceOptions { ImportId = $"/subscriptions/{providerConfig.Require("subscriptionId")}/resourceGroups/cok-memes", Protect = true }
    );

    var logAnalytics = new Workspace(
        "log-analytics",
        new WorkspaceArgs
        {
            ResourceGroupName = resourceGroup.Name,
            Sku = new WorkspaceSkuArgs { Name = WorkspaceSkuNameEnum.PerGB2018 },
            RetentionInDays = 30,
        }
    );

    var applicationInsights = new Component(
        "application-insights",
        new ComponentArgs
        {
            ApplicationType = ApplicationType.Other,
            Kind = "other",
            ResourceGroupName = resourceGroup.Name,
            WorkspaceResourceId = logAnalytics.Id,
        }
    );

    var logAnalyticsSharedKeys = Output
        .Tuple(resourceGroup.Name, logAnalytics.Name)
        .Apply(items => GetSharedKeys.InvokeAsync(new GetSharedKeysArgs { ResourceGroupName = items.Item1, WorkspaceName = items.Item2 }));

    var environment = new ManagedEnvironment(
        "environment",
        new ManagedEnvironmentArgs
        {
            ResourceGroupName = resourceGroup.Name,
            AppLogsConfiguration = new AppLogsConfigurationArgs
            {
                Destination = "log-analytics",
                LogAnalyticsConfiguration = new LogAnalyticsConfigurationArgs
                {
                    CustomerId = logAnalytics.CustomerId,
                    SharedKey = logAnalyticsSharedKeys.Apply(r => r.PrimarySharedKey!),
                },
            },
        }
    );

    const string imageRegistryServer = "docker.io";
    var imageRegistryUsername = config.Require("imageRegistryUsername");

    var dockerRegistryArgs = new RegistryArgs
    {
        Server = imageRegistryServer,
        Username = imageRegistryUsername,
        Password = config.RequireSecret("imageRegistryWritePassword"),
    };

    var botImage = new Image(
        "bot-image",
        new ImageArgs
        {
            ImageName = "uncledave/cok-bot:latest",
            Build = new DockerBuildArgs
            {
                Context = "..",
                Dockerfile = "../bot.Dockerfile",
                Platform = "linux/amd64",
            },
            Registry = dockerRegistryArgs,
        }
    );

    const string botTokenSecretName = "bot-token";
    const string imageRegistryReadPasswordSecretName = "registry-read-password";
    const string openAiApiKeySecretName = "open-ai-api-key";
    const string raidHelperApiKeySecretName = "raid-helper-api-key";
    const string mongoConnectionStringSecretName = "mongo-connection-string";
    const string applicationInsightsConnectionStringSecretName = "application-insights-connection-string";
    const string discordSerilogSinkWebhookIdSecretName = "discord-serilog-sink-webhook-id";
    const string discordSerilogSinkWebhookTokenSecretName = "discord-serilog-sink-webhook-token";
    const string googleSearchEngineApiKeySecretName = "google-search-engine-api-key";

    const string timezone = "Europe/Copenhagen";
    var dotnetEnvironment = config.Require("environment");
    var openAiApiKey = config.RequireSecret("openAiApiKey");
    var mongoConnectionString = config.RequireSecret("mongoConnectionString");

    var containerEnv = new List<EnvironmentVarArgs>
    {
        new() { Name = "TZ", Value = timezone },
        new() { Name = "Bot__Token", SecretRef = botTokenSecretName },
        new() { Name = "DOTNET_ENVIRONMENT", Value = dotnetEnvironment },
        new() { Name = "OpenAIServiceOptions__ApiKey", SecretRef = openAiApiKeySecretName },
        new() { Name = "RaidHelper__ApiKey", SecretRef = raidHelperApiKeySecretName },
        new() { Name = "ConnectionStrings__Mongo", SecretRef = mongoConnectionStringSecretName },
        new() { Name = "ConnectionStrings__ApplicationInsights", SecretRef = applicationInsightsConnectionStringSecretName },
        new() { Name = "DiscordSerilogSink__WebhookId", SecretRef = discordSerilogSinkWebhookIdSecretName },
        new() { Name = "DiscordSerilogSink__WebhookToken", SecretRef = discordSerilogSinkWebhookTokenSecretName },
        new() { Name = "GoogleSearchEngine__ApiKey", SecretRef = googleSearchEngineApiKeySecretName },
    };

    var commitSha = Environment.GetEnvironmentVariable("COMMIT_SHA");

    if (commitSha is not null)
        containerEnv.Add(new EnvironmentVarArgs { Name = "Bot__CommitSha", Value = commitSha });

    var containerApp = new ContainerApp(
        "bot-app",
        new ContainerAppArgs
        {
            ResourceGroupName = resourceGroup.Name,
            ManagedEnvironmentId = environment.Id,
            Configuration = new ConfigurationArgs
            {
                Registries = new RegistryCredentialsArgs
                {
                    Server = imageRegistryServer,
                    Username = imageRegistryUsername,
                    PasswordSecretRef = imageRegistryReadPasswordSecretName,
                },
                Secrets =
                {
                    new SecretArgs { Name = botTokenSecretName, Value = config.RequireSecret("botToken") },
                    new SecretArgs { Name = imageRegistryReadPasswordSecretName, Value = config.RequireSecret("imageRegistryReadPassword") },
                    new SecretArgs { Name = openAiApiKeySecretName, Value = openAiApiKey },
                    new SecretArgs { Name = raidHelperApiKeySecretName, Value = config.RequireSecret("raidHelperApiKey") },
                    new SecretArgs { Name = mongoConnectionStringSecretName, Value = mongoConnectionString },
                    new SecretArgs { Name = applicationInsightsConnectionStringSecretName, Value = applicationInsights.ConnectionString },
                    new SecretArgs { Name = discordSerilogSinkWebhookIdSecretName, Value = config.RequireSecret("discordSerilogSinkWebhookId") },
                    new SecretArgs
                    {
                        Name = discordSerilogSinkWebhookTokenSecretName,
                        Value = config.RequireSecret("discordSerilogSinkWebhookToken"),
                    },
                    new SecretArgs { Name = googleSearchEngineApiKeySecretName, Value = config.RequireSecret("googleSearchEngineApiKey") },
                },
            },
            Template = new TemplateArgs
            {
                Containers = new ContainerArgs
                {
                    Name = "bot",
                    Image = botImage.RepoDigest,
                    Env = containerEnv,
                    Resources = new ContainerResourcesArgs { Cpu = .25, Memory = "0.5Gi" },
                },
                Scale = new ScaleArgs { MinReplicas = 1, MaxReplicas = 1 },
            },
        }
    );

    var portalAppServicePlan = new AppServicePlan(
        "portal-app-service-plan",
        new AppServicePlanArgs
        {
            ResourceGroupName = resourceGroup.Name,
            Kind = "Linux",
            Reserved = true,
            Sku = new SkuDescriptionArgs { Name = "F1", Tier = "Free" },
        }
    );

    var portalImage = new Image(
        "portal-image",
        new ImageArgs
        {
            ImageName = "uncledave/cok-bot-portal:latest",
            Build = new DockerBuildArgs
            {
                Context = "..",
                Dockerfile = "../portal.Dockerfile",
                Platform = "linux/amd64",
            },
            Registry = dockerRegistryArgs,
        }
    );

    var portalAppSettings = new List<NameValuePairArgs>
    {
        new() { Name = "APPLICATIONINSIGHTS_CONNECTION_STRING", Value = applicationInsights.ConnectionString },
        new() { Name = "ApplicationInsightsAgent_EXTENSION_VERSION", Value = "~2" },
        new() { Name = "XDT_MicrosoftApplicationInsights_Mode", Value = "default" },
        new() { Name = "TZ", Value = timezone },
        new() { Name = "DOTNET_ENVIRONMENT", Value = dotnetEnvironment },
        new() { Name = "OpenAIServiceOptions__ApiKey", Value = openAiApiKey },
        new() { Name = "ConnectionStrings__Mongo", Value = mongoConnectionString },
        new() { Name = "Auth__ClientSecret", Value = config.RequireSecret("portalAuthClientSecret") },
        new() { Name = "WEBSITES_PORT", Value = "8080" },
        new() { Name = "ASPNETCORE_FORWARDEDHEADERS_ENABLED", Value = "true" },
    };

    if (commitSha is not null)
        portalAppSettings.Add(new NameValuePairArgs { Name = "CommitSha", Value = commitSha });

    var portalWebApp = new WebApp(
        "portal-app",
        new WebAppArgs
        {
            Name = "cok-bot",
            ResourceGroupName = resourceGroup.Name,
            ServerFarmId = portalAppServicePlan.Id,
            SiteConfig = new SiteConfigArgs
            {
                LinuxFxVersion = portalImage.RepoDigest.Apply(x => $"DOCKER|{x}"),
                AppSettings = portalAppSettings,
                FtpsState = FtpsState.Disabled,
            },
            HttpsOnly = true,
        },
        new CustomResourceOptions { Parent = portalAppServicePlan }
    );
});
