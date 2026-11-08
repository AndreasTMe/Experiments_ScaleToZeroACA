// jit waits 60 seconds after its last active poll before scaling to zero, instead of the default 300. The other apps
// keep the defaults.
using '../apps.bicep'
extends '../apps.bicepparam'

param scaleVariant = 'jit'
param scaleConfig = 'jit-cooldown-60'
param cooldownPeriod = 60
