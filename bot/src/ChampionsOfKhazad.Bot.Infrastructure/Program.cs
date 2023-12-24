using Pulumi;
using Pulumi.AzureNative.App;
using Pulumi.AzureNative.Insights;
using Pulumi.AzureNative.OperationalInsights;
using Pulumi.AzureNative.OperationalInsights.Inputs;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Storage;
using Pulumi.AzureNative.Storage.Inputs;
using Pulumi.AzureNative.Web;
using Pulumi.AzureNative.Web.Inputs;
using Pulumi.Command.Local;
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
using SkuName = Pulumi.AzureNative.Storage.SkuName;
using StorageAccountArgs = Pulumi.AzureNative.Storage.StorageAccountArgs;
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
            RetentionInDays = 30
        }
    );

    var applicationInsights = new Component(
        "application-insights",
        new ComponentArgs
        {
            ApplicationType = ApplicationType.Other,
            Kind = "other",
            ResourceGroupName = resourceGroup.Name,
            WorkspaceResourceId = logAnalytics.Id
        }
    );

    var logAnalyticsSharedKeys = Output
        .Tuple(resourceGroup.Name, logAnalytics.Name)
        .Apply(items => GetSharedKeys.InvokeAsync(new GetSharedKeysArgs { ResourceGroupName = items.Item1, WorkspaceName = items.Item2, }));

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
                    SharedKey = logAnalyticsSharedKeys.Apply(r => r.PrimarySharedKey!)
                }
            }
        }
    );

    const string imageRegistryServer = "docker.io";
    var imageRegistryUsername = config.Require("imageRegistryUsername");

    var botImage = new Image(
        "bot-image",
        new ImageArgs
        {
            ImageName = "uncledave/cok-bot:latest",
            Build = new DockerBuildArgs { Context = "..", Platform = "linux/amd64" },
            Registry = new RegistryArgs
            {
                Server = imageRegistryServer,
                Username = imageRegistryUsername,
                Password = config.RequireSecret("imageRegistryWritePassword")
            }
        }
    );

    const string botTokenSecretName = "bot-token";
    const string imageRegistryReadPasswordSecretName = "registry-read-password";
    const string openAiApiKeySecretName = "open-ai-api-key";
    const string raidHelperApiKeySecretName = "raid-helper-api-key";
    const string mongoConnectionStringSecretName = "mongo-connection-string";
    const string applicationInsightsConnectionStringSecretName = "application-insights-connection-string";

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
        new() { Name = "ConnectionStrings__ApplicationInsights", SecretRef = applicationInsightsConnectionStringSecretName }
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
                    PasswordSecretRef = imageRegistryReadPasswordSecretName
                },
                Secrets =
                {
                    new SecretArgs { Name = botTokenSecretName, Value = config.RequireSecret("botToken") },
                    new SecretArgs { Name = imageRegistryReadPasswordSecretName, Value = config.RequireSecret("imageRegistryReadPassword") },
                    new SecretArgs { Name = openAiApiKeySecretName, Value = openAiApiKey },
                    new SecretArgs { Name = raidHelperApiKeySecretName, Value = config.RequireSecret("raidHelperApiKey") },
                    new SecretArgs { Name = mongoConnectionStringSecretName, Value = mongoConnectionString },
                    new SecretArgs { Name = applicationInsightsConnectionStringSecretName, Value = applicationInsights.ConnectionString }
                }
            },
            Template = new TemplateArgs
            {
                Containers = new ContainerArgs
                {
                    Name = "bot",
                    Image = botImage.RepoDigest,
                    Env = containerEnv,
                    Resources = new ContainerResourcesArgs { Cpu = .25, Memory = "0.5Gi" }
                },
                Scale = new ScaleArgs { MinReplicas = 1, MaxReplicas = 1 }
            }
        }
    );

    var storageAccount = new StorageAccount(
        "storage",
        new StorageAccountArgs
        {
            ResourceGroupName = resourceGroup.Name,
            Kind = "StorageV2",
            Sku = new SkuArgs { Name = SkuName.Standard_LRS }
        }
    );

    var portalDeploymentsBlobContainer = new BlobContainer(
        "portal-deployments",
        new BlobContainerArgs
        {
            ResourceGroupName = resourceGroup.Name,
            AccountName = storageAccount.Name,
            PublicAccess = PublicAccess.None
        },
        new CustomResourceOptions { Parent = storageAccount }
    );

    var publishPortalCommand = new Command(
        "publish-portal",
        new CommandArgs
        {
            Create = "dotnet publish -c Production -r linux-x64",
            Dir = "../ChampionsOfKhazad.Bot.Portal",
            ArchivePaths = "bin/Production/net8.0/linux-x64/publish"
        }
    );

    var portalBlob = new Blob(
        "portal-deployment",
        new BlobArgs
        {
            ResourceGroupName = resourceGroup.Name,
            AccountName = storageAccount.Name,
            ContainerName = portalDeploymentsBlobContainer.Name,
            Type = BlobType.Block,
            Source = publishPortalCommand.Archive.Apply(x => (AssetOrArchive)x!)
        },
        new CustomResourceOptions { Parent = portalDeploymentsBlobContainer }
    );

    var portalAppServicePlan = new AppServicePlan(
        "portal-app-service-plan",
        new AppServicePlanArgs
        {
            ResourceGroupName = resourceGroup.Name,
            Kind = "Linux",
            Reserved = true,
            Sku = new SkuDescriptionArgs { Name = "F1", Tier = "Free" }
        }
    );

    var portalWebApp = new WebApp(
        "portal-app",
        new WebAppArgs
        {
            Name = "cok-bot",
            ResourceGroupName = resourceGroup.Name,
            ServerFarmId = portalAppServicePlan.Id,
            SiteConfig = new SiteConfigArgs
            {
                LinuxFxVersion = "DOTNETCORE|8.0",
                AppSettings =
                [
                    new NameValuePairArgs { Name = "APPLICATIONINSIGHTS_CONNECTION_STRING", Value = applicationInsights.ConnectionString },
                    new NameValuePairArgs { Name = "ApplicationInsightsAgent_EXTENSION_VERSION", Value = "~2" },
                    new NameValuePairArgs { Name = "XDT_MicrosoftApplicationInsights_Mode", Value = "default" },
                    new NameValuePairArgs { Name = "WEBSITE_RUN_FROM_PACKAGE", Value = portalBlob.Url },
                    new NameValuePairArgs { Name = "TZ", Value = timezone },
                    new NameValuePairArgs { Name = "DOTNET_ENVIRONMENT", Value = dotnetEnvironment },
                    new NameValuePairArgs { Name = "OpenAIServiceOptions__ApiKey", Value = openAiApiKey },
                    new NameValuePairArgs { Name = "ConnectionStrings__Mongo", Value = mongoConnectionString },
                    new NameValuePairArgs { Name = "Auth__ClientSecret", Value = config.RequireSecret("portalAuthClientSecret") }
                ],
                FtpsState = FtpsState.Disabled,
            },
            HttpsOnly = true,
        },
        new CustomResourceOptions { Parent = portalAppServicePlan }
    );
});
