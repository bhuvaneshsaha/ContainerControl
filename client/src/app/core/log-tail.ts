import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';

import { environment } from '../../environments/environment';

export function appendLogLine(current: string, line: string): string {
  const next = current.length === 0 ? line : `${current}\n${line}`;
  const rows = next.split('\n');
  return rows.length > 200 ? rows.slice(rows.length - 200).join('\n') : next;
}

export function startLogTail(
  appId: string,
  xsrfToken: string,
  onLine: (line: string) => void,
): Promise<HubConnection> {
  const connection = new HubConnectionBuilder()
    .withUrl(`${environment.apiUrl}/hubs/logs`, {
      withCredentials: true,
      headers: { 'X-XSRF-TOKEN': xsrfToken },
    })
    .build();
  connection.on('log', onLine);
  return connection.start().then(() => {
    void connection.invoke('Tail', appId);
    return connection;
  });
}
