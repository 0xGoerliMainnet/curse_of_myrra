defmodule DarkWorldsServerWeb.PlayWebSocket do
  @moduledoc """
  Play Websocket handler that parses msgs to be send to the runner genserver
  """
  alias DarkWorldsServer.Communication
  alias DarkWorldsServer.Engine
  alias DarkWorldsServer.Engine.RequestTracker
  alias DarkWorldsServer.Engine.Runner

  require Logger

  @behaviour :cowboy_websocket
  @ping_interval_ms 500
  # @server_hash Application.compile_env(:dark_worlds_server, :information) |> Keyword.get(:version_hash)

  @impl true
  def init(req, _opts) do
    game_id = :cowboy_req.binding(:game_id, req)
    player_id = :cowboy_req.binding(:player_id, req)
    client_id = :cowboy_req.binding(:client_id, req)
    client_hash = :cowboy_req.header("dark-worlds-client-hash", req)
    {:cowboy_websocket, req, %{game_id: game_id, player_id: player_id, client_id: client_id, client_hash: client_hash}}
  end

  @impl true
  def websocket_init(%{game_id: :undefined}) do
    {:stop, %{}}
  end

  def websocket_init(%{player_id: :undefined}) do
    {:stop, %{}}
  end

  def websocket_init(%{client_id: :undefined}) do
    {:stop, %{}}
  end

  # Uncomment to enable hash verification of client and server
  # def websocket_init(%{client_hash: hash}) when hash != @server_hash do
  #   {:stop, :version_mismatch}
  # end

  def websocket_init(%{game_id: game_id, player_id: player_id, client_id: client_id}) do
    runner_pid = Communication.external_id_to_pid(game_id)

    with :ok <- Phoenix.PubSub.subscribe(DarkWorldsServer.PubSub, "game_play_#{game_id}"),
         true <- runner_pid in Engine.list_runners_pids(),
         {:ok, player_id} <- Runner.join(runner_pid, client_id, String.to_integer(player_id)) do
      web_socket_state = %{runner_pid: runner_pid, player_id: player_id}

      Process.send_after(self(), :send_ping, @ping_interval_ms)

      {:ok, web_socket_state}
    else
      false ->
        {:stop, %{}}

      {:error, _reason} ->
        {:stop, %{}}
    end
  end

  @impl true
  def terminate(reason, _partialreq, %{runner_pid: pid, player_id: id}) do
    log_termination(reason)
    Runner.disconnect(pid, id)
    :ok
  end

  def terminate(:stop, _req, :version_mismatch) do
    Logger.info("#{__MODULE__} #{inspect(self())} closed because of server/client version mismatch")
    :ok
  end

  defp log_termination({_, 1000, _} = reason) do
    Logger.info("#{__MODULE__} with PID #{inspect(self())} closed with message: #{inspect(reason)}")
  end

  defp log_termination(reason) do
    Logger.error("#{__MODULE__} with PID #{inspect(self())} terminated with error: #{inspect(reason)}")
  end

  @impl true
  def websocket_handle({:binary, message}, web_socket_state) do
    case Communication.decode(message) do
      {:ok, action} ->
        RequestTracker.add_counter(web_socket_state[:runner_pid], web_socket_state[:player_id])
        Runner.play(web_socket_state[:runner_pid], web_socket_state[:player_id], action)
        {:ok, web_socket_state}

      {:error, msg} ->
        {:reply, {:text, "ERROR: #{msg}"}, web_socket_state}
    end
  end

  def websocket_handle(:pong, web_socket_state) do
    last_ping_time = web_socket_state.last_ping_time
    time_now = Time.utc_now()
    latency = Time.diff(time_now, last_ping_time, :millisecond)
    # Send back the player's ping
    {:reply, {:binary, Communication.encode!(%{latency: latency})}, web_socket_state}
  end

  def websocket_handle(_, web_socket_state) do
    {:reply, {:text, "ERROR unsupported message"}, web_socket_state}
  end

  @impl true
  def websocket_info({:player_joined, player_id}, web_socket_state) do
    {:reply, {:binary, Communication.game_player_joined(player_id)}, web_socket_state}
  end

  def websocket_info({:initial_positions, players}, web_socket_state) do
    {:reply, {:binary, Communication.initial_positions(players)}, web_socket_state}
  end

  # Send a ping frame every once in a while
  def websocket_info(:send_ping, web_socket_state) do
    Process.send_after(self(), :send_ping, @ping_interval_ms)
    time_now = Time.utc_now()
    {:reply, :ping, Map.put(web_socket_state, :last_ping_time, time_now)}
  end

  def websocket_info({:game_update, game_state}, web_socket_state) do
    reply_map = %{
      players: game_state.client_game_state.game.players,
      projectiles: game_state.client_game_state.game.projectiles,
      killfeed: game_state.client_game_state.game.killfeed,
      player_timestamp: game_state.player_timestamps[web_socket_state.player_id],
      playable_radius: game_state.client_game_state.game.playable_radius,
      shrinking_center: game_state.client_game_state.game.shrinking_center,
      server_timestamp: DateTime.utc_now() |> DateTime.to_unix(:millisecond)
    }

    {:reply, {:binary, Communication.game_update!(reply_map)}, web_socket_state}
  end

  def websocket_info({:game_finished, winner, game_state}, web_socket_state) do
    reply_map = %{
      players: game_state.client_game_state.game.players,
      winner: winner
    }

    Logger.info("THE GAME HAS FINISHED")

    {:reply, {:binary, Communication.game_finished!(reply_map)}, web_socket_state}
  end

  def websocket_info({:selected_characters, selected_characters}, web_socket_state) do
    {:reply, {:binary, Communication.selected_characters!(selected_characters)}, web_socket_state}
  end

  def websocket_info({:finish_character_selection, selected_players, players}, web_socket_state) do
    {:reply, {:binary, Communication.finish_character_selection!(selected_players, players)}, web_socket_state}
  end

  def websocket_info(info, web_socket_state), do: {:reply, {:text, info}, web_socket_state}
end
