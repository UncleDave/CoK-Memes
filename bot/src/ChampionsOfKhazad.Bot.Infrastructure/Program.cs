using Pulumi;
using Pulumi.AzureNative.App;
using Pulumi.AzureNative.App.Inputs;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Storage;
using Pulumi.AzureNative.Storage.Inputs;
using Pulumi.Docker;
using Pulumi.Docker.Inputs;
using Config = Pulumi.Config;
using ConfigurationArgs = Pulumi.AzureNative.App.Inputs.ConfigurationArgs;
using ContainerApp = Pulumi.AzureNative.App.ContainerApp;
using ContainerAppArgs = Pulumi.AzureNative.App.ContainerAppArgs;
using ContainerArgs = Pulumi.AzureNative.App.Inputs.ContainerArgs;
using ContainerResourcesArgs = Pulumi.AzureNative.App.Inputs.ContainerResourcesArgs;
using CustomDomainArgs = Pulumi.AzureNative.App.Inputs.CustomDomainArgs;
using EnvironmentVarArgs = Pulumi.AzureNative.App.Inputs.EnvironmentVarArgs;
using Kind = Pulumi.AzureNative.Storage.Kind;
using RegistryCredentialsArgs = Pulumi.AzureNative.App.Inputs.RegistryCredentialsArgs;
using ScaleArgs = Pulumi.AzureNative.App.Inputs.ScaleArgs;
using SecretArgs = Pulumi.AzureNative.App.Inputs.SecretArgs;
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

    var storageAccount = new StorageAccount(
        "storage",
        new StorageAccountArgs
        {
            ResourceGroupName = resourceGroup.Name,
            Location = resourceGroup.Location,
            AccessTier = AccessTier.Cold,
            Sku = new SkuArgs { Name = "Standard_LRS" },
            Kind = Kind.StorageV2,
            AllowBlobPublicAccess = true,
        },
        new CustomResourceOptions { Protect = true }
    );

    var storageAccountKey = ListStorageAccountKeys
        .Invoke(new ListStorageAccountKeysInvokeArgs { ResourceGroupName = resourceGroup.Name, AccountName = storageAccount.Name })
        .Apply(x => x.Keys.First().Value);

    var managedEnvironment = new ManagedEnvironment("environment", new ManagedEnvironmentArgs { ResourceGroupName = resourceGroup.Name });

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
    const string discordSerilogSinkWebhookIdSecretName = "discord-serilog-sink-webhook-id";
    const string discordSerilogSinkWebhookTokenSecretName = "discord-serilog-sink-webhook-token";
    const string googleSearchEngineApiKeySecretName = "google-search-engine-api-key";
    const string storageAccountAccessKeySecretName = "storage-account-access-key";
    const string portalAuthClientSecretName = "portal-auth-client-secret";
    const string mediatrLicenseKeySecretName = "mediatr-license-key";

    const string timezone = "Europe/Copenhagen";
    var dotnetEnvironment = config.Require("environment");
    var openAiApiKey = config.RequireSecret("openAiApiKey");
    var mongoConnectionString = config.RequireSecret("mongoConnectionString");
    var botToken = config.RequireSecret("botToken");
    var imageRegistryReadPassword = config.RequireSecret("imageRegistryReadPassword");

    var containerEnv = new List<EnvironmentVarArgs>
    {
        new() { Name = "TZ", Value = timezone },
        new() { Name = "Bot__Token", SecretRef = botTokenSecretName },
        new() { Name = "DOTNET_ENVIRONMENT", Value = dotnetEnvironment },
        new() { Name = "OpenAIServiceOptions__ApiKey", SecretRef = openAiApiKeySecretName },
        new() { Name = "RaidHelper__ApiKey", SecretRef = raidHelperApiKeySecretName },
        new() { Name = "ConnectionStrings__Mongo", SecretRef = mongoConnectionStringSecretName },
        new() { Name = "DiscordSerilogSink__WebhookId", SecretRef = discordSerilogSinkWebhookIdSecretName },
        new() { Name = "DiscordSerilogSink__WebhookToken", SecretRef = discordSerilogSinkWebhookTokenSecretName },
        new() { Name = "GoogleSearchEngine__ApiKey", SecretRef = googleSearchEngineApiKeySecretName },
        new() { Name = "AzureStorageAccountName", Value = storageAccount.Name },
        new() { Name = "AzureStorageAccountAccessKey", SecretRef = storageAccountAccessKeySecretName },
        new() { Name = "MediatR__LicenseKey", SecretRef = mediatrLicenseKeySecretName },
    };

    var commitSha = Environment.GetEnvironmentVariable("COMMIT_SHA");

    if (commitSha is not null)
        containerEnv.Add(new EnvironmentVarArgs { Name = "Bot__CommitSha", Value = commitSha });

    var registryCredentials = new RegistryCredentialsArgs
    {
        Server = imageRegistryServer,
        Username = imageRegistryUsername,
        PasswordSecretRef = imageRegistryReadPasswordSecretName,
    };

    var botContainerApp = new ContainerApp(
        "bot-app",
        new ContainerAppArgs
        {
            ResourceGroupName = resourceGroup.Name,
            ManagedEnvironmentId = managedEnvironment.Id,
            Configuration = new ConfigurationArgs
            {
                Registries = registryCredentials,
                Secrets =
                {
                    new SecretArgs { Name = botTokenSecretName, Value = botToken },
                    new SecretArgs { Name = imageRegistryReadPasswordSecretName, Value = imageRegistryReadPassword },
                    new SecretArgs { Name = openAiApiKeySecretName, Value = openAiApiKey },
                    new SecretArgs { Name = raidHelperApiKeySecretName, Value = config.RequireSecret("raidHelperApiKey") },
                    new SecretArgs { Name = mongoConnectionStringSecretName, Value = mongoConnectionString },
                    new SecretArgs { Name = discordSerilogSinkWebhookIdSecretName, Value = config.RequireSecret("discordSerilogSinkWebhookId") },
                    new SecretArgs
                    {
                        Name = discordSerilogSinkWebhookTokenSecretName,
                        Value = config.RequireSecret("discordSerilogSinkWebhookToken"),
                    },
                    new SecretArgs { Name = googleSearchEngineApiKeySecretName, Value = config.RequireSecret("googleSearchEngineApiKey") },
                    new SecretArgs { Name = storageAccountAccessKeySecretName, Value = storageAccountKey },
                    new SecretArgs { Name = mediatrLicenseKeySecretName, Value = config.RequireSecret("mediatrLicenseKey") },
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

    var portalContainerApp = new ContainerApp(
        "portal-app",
        new ContainerAppArgs
        {
            ResourceGroupName = resourceGroup.Name,
            ManagedEnvironmentId = managedEnvironment.Id,
            Configuration = new ConfigurationArgs
            {
                Registries = registryCredentials,
                Secrets =
                {
                    new SecretArgs { Name = botTokenSecretName, Value = botToken },
                    new SecretArgs { Name = imageRegistryReadPasswordSecretName, Value = imageRegistryReadPassword },
                    new SecretArgs { Name = mongoConnectionStringSecretName, Value = mongoConnectionString },
                    new SecretArgs { Name = openAiApiKeySecretName, Value = openAiApiKey },
                    new SecretArgs { Name = portalAuthClientSecretName, Value = config.RequireSecret("portalAuthClientSecret") },
                },
                Ingress = new IngressArgs
                {
                    External = true,
                    TargetPort = 8080,
                    Transport = IngressTransportMethod.Http,
                    AllowInsecure = false,
                    CustomDomains =
                    [
                        new CustomDomainArgs
                        {
                            Name = "bot.championsofkhazad.com",
                            CertificateId = GetManagedCertificate
                                .Invoke(
                                    new GetManagedCertificateInvokeArgs
                                    {
                                        ResourceGroupName = resourceGroup.Name,
                                        EnvironmentName = managedEnvironment.Name,
                                        ManagedCertificateName = "bot.championsofkhazad.com-environm-250829224145",
                                    }
                                )
                                .Apply(x => x.Id),
                        },
                    ],
                },
            },
            Template = new TemplateArgs
            {
                Containers = new ContainerArgs
                {
                    Name = "portal",
                    Image = portalImage.RepoDigest,
                    Env =
                    [
                        new EnvironmentVarArgs { Name = "TZ", Value = timezone },
                        new EnvironmentVarArgs { Name = "DOTNET_ENVIRONMENT", Value = dotnetEnvironment },
                        new EnvironmentVarArgs { Name = "OpenAi__ApiKey", SecretRef = openAiApiKeySecretName },
                        new EnvironmentVarArgs { Name = "ConnectionStrings__Mongo", SecretRef = mongoConnectionStringSecretName },
                        new EnvironmentVarArgs { Name = "Auth__ClientSecret", SecretRef = portalAuthClientSecretName },
                        new EnvironmentVarArgs { Name = "ASPNETCORE_FORWARDEDHEADERS_ENABLED", Value = "true" },
                        new EnvironmentVarArgs { Name = "BotToken", SecretRef = botTokenSecretName },
                    ],
                    Resources = new ContainerResourcesArgs { Cpu = .25, Memory = "0.5Gi" },
                },
                Scale = new ScaleArgs { MinReplicas = 1, MaxReplicas = 1 },
            },
        }
    );
});
