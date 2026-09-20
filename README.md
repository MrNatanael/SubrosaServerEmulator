## Subrosa Server Emulator

This project allows you to host your own master server for Subrosa.

**Keep in mind that this is W.I.P. and may fail in some cases.**

> [!WARNING]
> Currently, the master server generates player IDs using the player's nickname. If the nickname changes, a new ID will be assigned. I'm not entirely sure what effects this could have. It may or may not be fixed in the future.

Setting up the master server requires a couple of steps. Please read the instructions carefully.

Start by cloning this repository:

`git clone https://github.com/MrNatanael/SubrosaServerEmulator.git`

Then follow the steps below.

If you only want to connect to a third-party master server, you can go to **Setting up the Client**.

#### Setting up the Master Server
If you are going to run on Docker:
  - Inside the `docker` folder, create a copy of the `default.config.json` file and rename it to `config.json`.

If you are going to compile it yourself on run it without Docker:
  - After the first execution, the file `config.json` will be generated inside the running directory.

The `config.json` file contains all the parameters that will allow you to host a master server. The parameters are as follows:

* `master_listen_ip`: The IP address the master server will be bound to. If you are running inside Docker, set it to `0.0.0.0`.
* `master_public_ip`: Set this to the server's public IP address.
* `web_server_listen_ip`: The IP address the web server will be bound to. If you are running inside Docker, set it to `0.0.0.0`.
* `master_listen_port`: The port the master server will be bound to. If you change this and are running inside Docker, make sure to change it in `docker-compose.yml` as well.
* `web_server_listen_port`: The port the web server will be bound to.
* `log_level`: Sets the minimum level of the logs:

  * `0 - Debug`
  * `1 - Info`
  * `2 - Warning`
  * `3 - Error`

After updating the settings as desired, either run `docker-compose up -d` inside the `docker` folder, or compile and execute `SubrosaServerEmulator`.

These are all the steps needed to have a working master server. Next, you need to set up the client.

### Setting up the Client

> [!WARNING]
> This step is crucial for the game to work. Don't skip it, or you will be unable to connect.

Inside the `SubrosaWebIntercept` folder, you will find the files `compile.sh` and `intercept.cfg`.

> Currently, you need to compile the binaries yourself. Pre-built binaries may be available in the future.

Start by running `compile.sh` to compile the required mod for the game client to work. If you don't want to compile for Windows, simply comment out the MinGW command.

After compiling, the binaries can be found inside the `bin` folder. Copy the desired binary (`hook.so` on Linux or `WSOCK32.dll` on Windows) and the `intercept.cfg` file to your game root folder.

Then, open the `intercept.cfg` file you just copied and update it as described below:

* The first line is the **IP address** of the web server, e.g. `192.168.0.100`.
* The second line is the **port** of the web server, e.g. `80`.

**DO NOT CHANGE THE ORDER OF THESE FIELDS, AND DO NOT ADD EMPTY LINES AT THE TOP.**

If you are on Linux, you will need to add the following parameter at the beginning of the launch arguments:

`LD_PRELOAD=/path/to/subrosa/hook.so`

For example:

`LD_PRELOAD=/home/username/.local/share/Steam/steamapps/common/Subrosa/hook.so`

After that, you can simply open the game and start playing.
