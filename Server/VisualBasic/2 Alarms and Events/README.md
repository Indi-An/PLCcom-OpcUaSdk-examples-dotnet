# 2 Alarms and Events – Server Examples

This folder contains examples for publishing OPC UA events and alarms from a server.
Each subfolder is a standalone sample project.

---

## Examples

### 21_Simple_Events
Enables event notifications on a node and fires `BaseEventType` events with different severity
levels (Low / Medium / High, range 1–1000). Events propagate up to the Server node, so clients
can subscribe to either the source node or the Server node to receive them.

Endpoint: `opc.tcp://localhost:48420`

### 22_Alarm_Conditions
Implements the OPC UA Alarms & Conditions model (Part 9). Creates stateful alarms on a reactor
node that activate and deactivate based on simulated process values (temperature, pressure).
Demonstrates hysteresis logic, alarm severity, and the Retain flag that controls visibility in
the client's Alarm & Conditions view. Clients can acknowledge alarms interactively.

Endpoint: `opc.tcp://localhost:48421`
