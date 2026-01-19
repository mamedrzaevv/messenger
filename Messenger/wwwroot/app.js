const statusEl = document.getElementById("status");
const authPanel = document.getElementById("auth-panel");
const authScreen = document.getElementById("auth-screen");
const workspace = document.getElementById("workspace");
const loginForm = document.getElementById("login-form");
const registerForm = document.getElementById("register-form");
const loginName = document.getElementById("login-name");
const loginPassword = document.getElementById("login-password");
const registerName = document.getElementById("register-name");
const registerPassword = document.getElementById("register-password");
const loginPanel = document.getElementById("login-panel");
const registerPanel = document.getElementById("register-panel");
const showRegisterButton = document.getElementById("show-register");
const hideRegisterButton = document.getElementById("hide-register");
const currentUserEl = document.getElementById("current-user");
const chatItemsEl = document.getElementById("chat-items");
const userSearchInput = document.getElementById("user-search");
const searchUserButton = document.getElementById("search-user");
const searchResultsEl = document.getElementById("search-results");
const logoutButton = document.getElementById("logout");

const setStatusOnline = () => updateStatus("Online", true);

const updateStatus = (text, isReady) => {
  statusEl.textContent = text;
  statusEl.style.background = isReady ? "#e7f6ee" : "#f2f4f7";
  statusEl.style.color = isReady ? "#1b6b3d" : "#425466";
};

const setStatusVisible = (isVisible) => {
  statusEl.classList.toggle("hidden", !isVisible);
};

const setWorkspaceVisible = (isVisible) => {
  authScreen.hidden = isVisible;
  workspace.classList.toggle("hidden", !isVisible);
};

const setRegisterVisible = (isVisible) => {
  registerPanel.classList.toggle("hidden", !isVisible);
  loginPanel.classList.toggle("hidden", isVisible);
  showRegisterButton.classList.toggle("hidden", isVisible);
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

const loadMe = async () => {
  try {
    return await api("/api/me");
  } catch {
    return null;
  }
};

const loadChats = async () => {
  const chats = await api("/api/chats");
  chatItemsEl.innerHTML = "";
  chats.forEach((chat) => {
    const item = document.createElement("li");
    item.textContent = chat.title;
    item.dataset.chatId = chat.id;
    item.addEventListener("click", () => openChat(chat));
    chatItemsEl.appendChild(item);
  });
};

const renderSearchResults = (results) => {
  searchResultsEl.innerHTML = "";
  if (!results.length) {
    const empty = document.createElement("li");
    empty.textContent = "Ничего не найдено";
    searchResultsEl.appendChild(empty);
    return;
  }

  results.forEach((user) => {
    const item = document.createElement("li");
    item.textContent = user.userName;
    const button = document.createElement("button");
    button.type = "button";
    button.textContent = "Создать чат";
    button.addEventListener("click", () => createChatWithUser(user.userName));
    item.appendChild(button);
    searchResultsEl.appendChild(item);
  });
};

const createChatWithUser = async (userName) => {
  try {
    const chat = await api("/api/chats", {
      method: "POST",
      body: JSON.stringify({
        title: userName,
        userNames: [userName],
      }),
    });
    await loadChats();
    openChat(chat);
  } catch (error) {
    alert(error.message);
  }
};

const openChat = (chat) => {
  window.location.href = `chat.html?chatId=${encodeURIComponent(chat.id)}`;
};

loginForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  try {
    await api("/api/auth/login", {
      method: "POST",
      body: JSON.stringify({
        userName: loginName.value.trim(),
        password: loginPassword.value,
      }),
    });
    loginPassword.value = "";
    await bootstrap();
  } catch (error) {
    alert(error.message);
  }
});

registerForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  try {
    await api("/api/auth/register", {
      method: "POST",
      body: JSON.stringify({
        userName: registerName.value.trim(),
        password: registerPassword.value,
      }),
    });
    registerPassword.value = "";
    await bootstrap();
  } catch (error) {
    alert(error.message);
  }
});

showRegisterButton.addEventListener("click", () => {
  setRegisterVisible(true);
});

hideRegisterButton.addEventListener("click", () => {
  setRegisterVisible(false);
});

logoutButton.addEventListener("click", async () => {
  await api("/api/auth/logout", { method: "POST" });
  setWorkspaceVisible(false);
  updateStatus("Offline", false);
});

searchUserButton.addEventListener("click", async () => {
  const query = userSearchInput.value.trim();
  if (!query) {
    return;
  }
  try {
    const results = await api(`/api/users/search?query=${encodeURIComponent(query)}`);
    renderSearchResults(results);
  } catch (error) {
    alert(error.message);
  }
});

const bootstrap = async () => {
  const me = await loadMe();
  if (!me?.id) {
    setWorkspaceVisible(false);
    setStatusVisible(false);
    updateStatus("Offline", false);
    return;
  }

  currentUserEl.textContent = me.userName ?? "user";
  setWorkspaceVisible(true);
  setRegisterVisible(false);
  setStatusVisible(true);
  setStatusOnline();
  await loadChats();
};

setWorkspaceVisible(false);
setRegisterVisible(false);
updateStatus("Offline", false);
setStatusVisible(false);

bootstrap();
