// jit's scale rule polls its queue every 5 seconds. The other apps keep the defaults.
using '../apps.bicep'
extends '../apps.bicepparam'

param scaleVariant = 'jit'
param scaleConfig = 'jit-polling-5'
param pollingInterval = 5
