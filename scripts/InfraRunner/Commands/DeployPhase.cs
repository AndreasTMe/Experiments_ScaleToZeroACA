namespace InfraRunner.Commands;

internal enum DeployPhase
{
    // infra/main.bicep at subscription scope: the resource group, budget, registry and Service Bus.
    Base,

    // infra/apps.bicep in the resource group: the Container Apps environment and one app per variant.
    Apps
}