# fim-queueing-admin

Detailed docs to be created later

## Running Locally

Run the following command with the `gcloud` CLI so the app picks up credentials:

```bash
gcloud auth application-default login
```


## SignalR Messages

### DisplayHub

The DisplayHub is for interfacing with instances of `fim-queueing`.

Server-to-Client

| Method       | Param 1         |
|--------------|-----------------|
| SendRefresh  | n/a             |
| Identify     | n/a             |
| SendNewRoute | `string`: Route |

Client-to-Server

| Method      | Param 1                                                                                      |
|-------------|----------------------------------------------------------------------------------------------|
| UpdateInfo  | `{eventKey: string, eventCode: string, route: string, installationId: string}`: Display Info |
| UpdateRoute | `string`: Route                                                                              |

### AssistantHub

TODO

