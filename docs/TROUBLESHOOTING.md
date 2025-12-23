# Troubleshooting

Common issues and solutions for WMI Explorer.

## Table of Contents

- [Connection Issues](#connection-issues)
- [Query Issues](#query-issues)
- [Event Watcher Issues](#event-watcher-issues)

## Connection Issues

### "Access Denied"
- Run the application as Administrator
- Verify your user account has WMI access permissions

### "Invalid Namespace"
- Verify the namespace path is correct (e.g., `root\cimv2`)
- Check that the namespace exists on the target computer
- Ensure you have access to the namespace

### Remote Connection Fails
- Ensure Windows Firewall allows WMI traffic (port 135 and dynamic ports)
- Verify credentials have WMI access on the remote computer
- Check DCOM configuration for remote access
- Verify the remote computer is accessible on the network
- Try using the connection options dialog to specify authentication and impersonation settings

## Query Issues

### "Invalid Query"
- Verify WQL syntax is correct
- Check that the class name exists in the selected namespace
- Ensure property names are spelled correctly
- Review the query for syntax errors (missing quotes, incorrect operators, etc.)

### "No Results"
- The query may be valid but return no matching instances
- Try removing WHERE clauses to see if any instances exist
- Verify you're querying the correct namespace
- Check that the class has instances (some classes are abstract and have no instances)

### Slow Queries
- Some queries can take time on large namespaces
- Use filters (WHERE clauses) to narrow results
- Avoid `SELECT *` on classes with many properties
- Consider querying specific properties instead of all properties

## Event Watcher Issues

### No Events Received
- Verify the event class exists in the namespace
- Check that events are actually occurring (e.g., if monitoring process creation, try creating a process)
- Some events require administrator privileges
- Verify the target class exists and is accessible
- Check that the event class and target class are in the same namespace
- Ensure the watcher is started (not paused)

### Events Stop Appearing
- Check if the watcher was stopped or removed
- Verify the connection to the WMI namespace is still active
- Some events may have filters that are too restrictive
- Try removing property filters to see if events resume

