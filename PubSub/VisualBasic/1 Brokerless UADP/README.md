# 1 Brokerless UADP — PubSub (Visual Basic)

These workshops demonstrate brokerless OPC UA PubSub communication using UDP with UADP binary encoding. No broker is required — publisher and subscriber communicate directly over UDP.

| # | Workshop | What you will learn |
|---|----------|-------------------|
| 11 | UADP Unicast Publisher | Publish data directly to a single subscriber via UDP unicast |
| 12 | UADP Unicast Subscriber | Receive unicast messages with dynamic field discovery |
| 13 | UADP Multicast Publisher | Publish to a multicast group — multiple subscribers receive simultaneously |
| 14 | UADP Multicast Subscriber | Join a multicast group and receive published data |
| 15 | UADP Broadcast Publisher | Publish UADP messages to the local broadcast address |
| 16 | UADP Broadcast Subscriber | Receive broadcast UADP messages without discovery |

**Pairing:** Run Workshop 11 + 12 together, Workshop 13 + 14 together, or Workshop 15 + 16 together.

**Requires:** [PLCcom.Opc.Ua.PubSub](https://www.nuget.org/packages/PLCcom.Opc.Ua.PubSub) NuGet package.
