# ADR 0012: Engine endpoint schemes are allow-listed

## Status
Accepted

## Context
Host registration and the Engine client accepted any absolute URI. The client can open more transports than a local socket, a Windows named pipe, or a remote Engine TCP endpoint. A scheme such as `file`, `http`, `https`, or `ssh`, or an address with no scheme, would leave that path open.

## Decision
`EngineConnectUri` is the only scheme check. Host registration and `DockerEngineClient` both use it before a host row is stored and before a client is created. The allowed schemes are `unix`, `npipe`, and `tcp`, compared without regard to case. Anything else is rejected with a fixed message that does not include the supplied address. TLS for `tcp` is unchanged: this decision does not add certificates and does not change how `DockerClientConfiguration` is built. See ADR 0002.

## Consequences
Operators register `unix:///var/run/docker.sock`, `npipe://./pipe/docker_engine`, or `tcp://host:port`. `http` and `https` addresses are not accepted. A rejected address is not stored. A connect that still carries a disallowed URI fails before a client is created.
