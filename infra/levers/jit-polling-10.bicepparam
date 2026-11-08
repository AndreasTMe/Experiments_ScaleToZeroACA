// jit's scale rule polls its queue every 10 seconds. The other apps keep the defaults.
using '../apps.bicep'
extends '../apps.bicepparam'

param scaleVariant = 'jit'
param scaleConfig = 'jit-polling-10'
param pollingInterval = 10
