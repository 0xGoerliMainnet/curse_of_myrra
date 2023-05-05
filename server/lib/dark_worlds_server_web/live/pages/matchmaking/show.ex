defmodule DarkWorldsServerWeb.MatchmakingLive.Show do
  use DarkWorldsServerWeb, :live_view
  alias DarkWorldsServer.Communication
  alias DarkWorldsServer.Matchmaking

  def mount(%{"session_id" => session_id}, _session, socket) do
    case connected?(socket) do
      false ->
        {:ok, assign(socket, session_id: session_id, player_count: 1)}

      true ->
        # TODO: Replace this by a proper player_id once we use actual accounts
        player_id = "player_id_#{:rand.uniform(1000)}"
        Phoenix.PubSub.subscribe(DarkWorldsServer.PubSub, Matchmaking.session_topic(session_id))
        session_pid = Communication.external_id_to_pid(session_id)
        Matchmaking.add_player(player_id, session_pid)

        {:ok,
         assign(socket,
           session_id: session_id,
           player_id: player_id,
           player_count: 1,
           session_pid: session_pid
         )}
    end
  end

  def handle_event("start_game", _params, socket) do
    Matchmaking.start_game(socket.assigns[:session_pid])
    {:noreply, socket}
  end

  def handle_info({:player_added, player_count}, socket) do
    {:noreply, assign(socket, :player_count, player_count)}
  end

  def handle_info({:player_removed, player_count}, socket) do
    {:noreply, assign(socket, :player_count, player_count)}
  end

  def handle_info({:game_started, game_pid}, socket) do
    {:noreply, redirect(socket, to: ~p"/board/#{Communication.pid_to_external_id(game_pid)}")}
  end

  def handle_info({:ping, pid}, socket) do
    send(pid, :pong)
    {:noreply, socket}
  end

  def terminate(_reason, socket) do
    Matchmaking.remove_player(socket.assigns[:player_id], socket.assigns[:session_pid])
    :ignored
  end
end