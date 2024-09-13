using Chrysalis.Cardano.Models.Cbor;
using Chrysalis.Cbor;

namespace Chrysalis.Cardano.Models.Core.Transaction;

[CborSerializable(CborType.List)]
public record VKeyWitness(
    CborBytes VKey,
    CborBytes Signature
) : ICbor;