# 2 Broker UADP — PubSub (C#)

These workshops demonstrate broker-based OPC UA PubSub communication using MQTT with UADP binary encoding. An MQTT broker (e.g. Eclipse Mosquitto) is required.

| # | Workshop | What you will learn |
|---|----------|-------------------|
| 21 | MQTT UADP Publisher | Publish data to an MQTT broker using compact UADP binary encoding |
| 22 | MQTT UADP Subscriber | Receive UADP messages from an MQTT broker |
| 23 | sMQTT UADP Publisher | Publish over MQTT with TLS encryption (mqtts://) |
| 24 | sMQTT UADP Subscriber | Receive UADP messages over a TLS-secured MQTT connection |

**Pairing:** Run Workshop 21 + 22 together, or Workshop 23 + 24 together.

**Prerequisites:** An MQTT broker running on `localhost:1883` (plain) or `localhost:8883` (TLS).

**Requires:** [PLCcom.Opc.Ua.PubSub](https://www.nuget.org/packages/PLCcom.Opc.Ua.PubSub) NuGet package.
