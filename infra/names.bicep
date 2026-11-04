// Resource names shared by the base and apps templates, so the apps can find the base resources without passing
// deployment outputs around.

@export()
type ResourceNames = {
  pullIdentity: string
  registry: string
  serviceBusNamespace: string
  workspace: string
  environment: string
}

@export()
func resourceNames(namePrefix string, resourceGroupId string) ResourceNames => {
  pullIdentity: 'id-${namePrefix}-pull'
  registry: 'acr${namePrefix}${uniqueString(resourceGroupId)}'
  serviceBusNamespace: 'sb-${namePrefix}-${uniqueString(resourceGroupId)}'
  workspace: 'log-${namePrefix}'
  environment: 'cae-${namePrefix}'
}
