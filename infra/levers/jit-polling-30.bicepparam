// jit's scale rule polls its queue every 30 seconds, the platform default, set explicitly so the request carries it.
// The other apps keep the defaults.
using '../apps.bicep'
extends '../apps.bicepparam'

param scaleVariant = 'jit'
param scaleConfig = 'jit-polling-30'
param pollingInterval = 30
