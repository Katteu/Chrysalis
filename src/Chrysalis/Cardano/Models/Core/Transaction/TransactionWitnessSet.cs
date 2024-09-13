using Chrysalis.Cbor;
using Chrysalis.Cardano.Models.Cbor;
using Chrysalis.Cardano.Models.Core.Script;

namespace Chrysalis.Cardano.Models.Core.Transaction;

[CborSerializable(CborType.Map)]
public record TransactionWitnessSet(
    [CborProperty(0)] CborDefiniteList<VKeyWitness>? VKeyWitnessSet,
    [CborProperty(1)] CborDefiniteList<NativeScript>? NativeScriptSet,
    [CborProperty(2)] CborDefiniteList<BootstrapWitness>? BootstrapWitnessSet,
    [CborProperty(3)] CborDefiniteList<BootstrapWitness>? PlutusV1ScriptSet, // @TODO: Modify T Parameter    
    [CborProperty(4)] CborDefiniteList<BootstrapWitness>? PlutusDataSet, // @TODO: Modify T Parameter   
    [CborProperty(5)] Redeemers? Redeemers,
    [CborProperty(6)] CborDefiniteList<BootstrapWitness>? PlutusV2ScriptSet, // @TODO: Modify T Parameter
    [CborProperty(7)] CborDefiniteList<BootstrapWitness>? PlutusV3ScriptSet // @TODO: Modify T Parameter
) : ICbor;