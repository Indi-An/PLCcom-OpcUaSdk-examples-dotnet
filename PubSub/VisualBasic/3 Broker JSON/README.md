# 3 Broker JSON — PubSub (Visual Basic)

These workshops demonstrate broker-based OPC UA PubSub communication using MQTT with JSON encoding. JSON messages are human-readable and can be consumed by any MQTT client — not just OPC UA applications.

| # | Workshop | What you will learn |
|---|----------|-------------------|
| 31 | MQTT JSON Publisher | Publish data to an MQTT broker using human-readable JSON encoding |
| 32 | MQTT JSON Subscriber | Receive JSON messages from an MQTT broker |
| 33 | sMQTT JSON Publisher | Publish JSON over MQTT with TLS encryption (mqtts://) |
| 34 | sMQTT JSON Subscriber | Receive JSON messages over a TLS-secured MQTT connection |

**Pairing:** Run Workshop 31 + 32 together, or Workshop 33 + 34 together.

**Prerequisites:** An MQTT broker running on `localhost:1883` (plain) or `localhost:8883` (TLS).

**Tip:** Use [MQTT Explorer](https://mqtt-explorer.com/) to inspect the JSON messages live on the broker.

**Requires:** [PLCcom.Opc.Ua.PubSub](https://www.nuget.org/packages/PLCcom.Opc.Ua.PubSub) NuGet package.
