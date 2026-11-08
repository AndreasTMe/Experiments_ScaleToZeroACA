// jit always keeps one replica running, so it never scales to zero. The other apps keep the defaults.
using '../apps.bicep'
extends '../apps.bicepparam'

param scaleVariant = 'jit'
param scaleConfig = 'jit-min-1'
param minReplicas = 1
