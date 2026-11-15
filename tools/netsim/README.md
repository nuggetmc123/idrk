# netsim - multiplayer test without headsets

Runs the real `Fusion/FusionBackend.cs` with 3-4 simulated players in one process, on a fake Photon
(`src/FakeFusion.cs`) that can misbehave: label every packet as coming from the master client, drop data between
non-master players, and have the master leave mid-raid. Each player sends a "pose" saying who it really is; the
test checks every player hears every other one, under one id each, with the right callsign.

```bash
cd tools/netsim
mkdir -p game && cp ../../RagoExtract/Assets/RagoExtract/Fusion/FusionBackend.cs ../../RagoExtract/Assets/RagoExtract/Runtime/Net/NetApi.cs game/
dotnet run            # transport: every player hears every other one, right ids and names
dotnet run -- queue   # matchmaking: parties + solo players all land in the same raid, with correct spawns (~5 min, real time)
```
`src/Shim.cs` / `src/TimeShim.cs` are a tiny stand-in for UnityEngine (enough to run the backend).
