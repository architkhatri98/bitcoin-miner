# bitcoin-miner

How to run code:
1. Server File:
Open terminal in the same master folder.
Run command: dotnet fsi server.fsx 4 (4 is the no. of leading zeroes and user can input their own required number of leading zeroes in the args at the end of the line).


2. Client File:
Open the terminal on worker-laptop.
Run command: dotnet fsi client.fsx 127.0.0.1 (127.0.0.1 is the IP address of the server)
