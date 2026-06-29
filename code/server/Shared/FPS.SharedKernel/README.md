# FairSpot.SharedKernel

Shared kernel for [FairSpot](https://github.com/RobertVejvoda/fairspot) — the cross-cutting
primitives reused across services, most notably the **identity / authorization mechanism**:
multi-issuer JWT bearer configuration, the tenant/platform claims transformation, and the
canonical FairSpot role names.

Published as an internal package so the private FairSpot **platform** service can consume the
same auth mechanism as the open core. Within the open repository, services reference this
project directly (`ProjectReference`) rather than the package.

Licensed under **AGPL-3.0-or-later** (matches the FairSpot repository).
