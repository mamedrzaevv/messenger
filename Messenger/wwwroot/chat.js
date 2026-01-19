const statusEl = document.getElementById("status");
const messagesEl = document.getElementById("messages");
const formEl = document.getElementById("chat-form");
const messageInput = document.getElementById("message");
const chatTitleCurrent = document.getElementById("chat-title-current");

const params = new URLSearchParams(window.location.search);
const chatId = params.get("chatId");

let connection = null;

const updateStatus = (text, isReady) => {
  statusEl.textContent = text;
  statusEl.style.background = isReady ? "#e7f6ee" : "#f2f4f7";
  statusEl.style.color = isReady ? "#1b6b3d" : "#425466";
};

const api = async (url, options = {}) => {
  const response = await fetch(url, {
    headers: { "Content-Type": "application/json" },
    credentials: "same-origin",
    ...options,
  });

  const payload = await response.json().catch(() => ({}));
  if (!response.ok) {
    throw new Error(payload.error || "Request failed");
  }
  return payload;
};

const formatTime = (timestamp) => {
  const date = new Date(timestamp);
  return date.toLocaleTimeString("ru-RU", {
    hour: "2-digit",
    minute: "2-digit",
  });
};

const appendMessage = (payload) => {
  const li = document.createElement("li");
  li.className = "message";

  const meta = document.createElement("div");
  meta.className = "meta";
  meta.textContent = payload.userName ?? "user";

  const time = document.createElement("span");
  time.textContent = formatTime(payload.sentAt);
  meta.appendChild(time);

  const text = document.createElement("div");
  text.className = "text";
  text.textContent = payload.text;

  li.appendChild(meta);
  li.appendChild(text);
  messagesEl.appendChild(li);
  messagesEl.scrollTop = messagesEl.scrollHeight;
};

const loadChatTitle = async () => {
  const chats = await api("/api/chats");
  const current = chats.find((chat) => chat.id === chatId);
  chatTitleCurrent.textContent = current ? current.title : "Чат";
};

const loadMessages = async () => {
  const messages = await api(`/api/chats/${chatId}/messages`);
  messagesEl.innerHTML = "";
  messages.forEach(appendMessage);
};

const initConnection = () => {
  connection = new signalR.HubConnectionBuilder()
    .withUrl("/chat")
    .withAutomaticReconnect()
    .build();

  connection.on("ReceiveMessage", (payload) => {
    if (payload.chatId === chatId) {
      appendMessage(payload);
    }
  });

  connection.onreconnecting(() => {
    updateStatus("Reconnecting…", false);
  });

  connection.onreconnected(async () => {
    updateStatus("Online", true);
    await connection.invoke("JoinChat", chatId);
  });

  connection.onclose(() => {
    updateStatus("Offline", false);
  });
};

const startConnection = async () => {
  if (!connection) {
    initConnection();
  }

  try {
    await connection.start();
    await connection.invoke("JoinChat", chatId);
    updateStatus("Online", true);
  } catch (error) {
    updateStatus("Offline", false);
    setTimeout(startConnection, 2000);
  }
};

formEl.addEventListener("submit", async (event) => {
  event.preventDefault();
  const message = messageInput.value.trim();
  if (!message) {
    return;
  }

  try {
    await connection.invoke("SendMessage", chatId, message);
    messageInput.value = "";
    messageInput.focus();
  } catch (error) {
    console.error(error);
  }
});

const bootstrap = async () => {
  if (!chatId) {
    chatTitleCurrent.textContent = "Чат не найден";
    return;
  }

  try {
    await api("/api/me");
  } catch {
    window.location.href = "/";
    return;
  }

  await loadChatTitle();
  await loadMessages();
  await startConnection();
};

bootstrap();
