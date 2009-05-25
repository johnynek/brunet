/*
Copyright (C) 2009 Pierre St Juste <ptony82@ufl.edu>, University of Florida

This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
*/

var stateXML = "";
var pageID = "friends";
var option = "All";

init();

function init() {
  getState();
  loadPage();
  loadHeader();
  var intervalID = window.setInterval(getState, 15000);
}

function createElem(itemType, itemHTML, itemID, itemClass, containerName, 
                    functionName) {
  var elem = document.createElement(itemType);
  elem.id = itemID;
  elem.className = itemClass;
  if(functionName != "") {
    elem.addEventListener("click", functionName, false);
  }
  elem.innerHTML = itemHTML;
  if(typeof containerName == 'string') {
    var container = document.getElementById(containerName); 
    container.appendChild(elem);
  }
  else {
    containerName.appendChild(elem);
  }
  return elem;
}

function clearDiv(name) {
  var div_main_content = document.getElementById(name);
  div_main_content.innerHTML = "";
}

function createTable(title) {
  createElem("table", "", "data_table", "", "main_content", "");
  createElem("tr", "", "table_tr", "", "data_table", "");
  createElem("td", "<b>"+ title + "</b>", "", "", "table_tr", "");
}

function loadPage() {
  document.title = "SocialVPN"; 
  var headElement = document.getElementsByTagName('head')[0];
  var styleSheet = document.createElement('link');
  styleSheet.setAttribute('rel', 'stylesheet');
  styleSheet.setAttribute('type', 'text/css');
  styleSheet.setAttribute('href', 'socialvpn.css');
  headElement.appendChild(styleSheet);
  
  document.body.innerHTML = '';
  document.body = document.createElement('body');

  var div_wrapper = document.createElement('div');
  div_wrapper.id = "wrapper";
  document.body.appendChild(div_wrapper);
  
  createElem("div", "","header","", "wrapper", "");
  createElem("div", "", "subheader","", "header", "");
  createElem("div", "", "main_body", "", "wrapper", "");
  createElem("div", "", "local_nav", "", "main_body", "");
  createElem("div", "", "tmp_content", "", "main_body", "");
  createElem("div", "", "main_content", "", "main_body", "");
}

function loadHeader() {
  var main_title = document.createElement('span');
  main_title.className = "svpn";
  main_title.innerHTML = "SocialVPN";

  var menu = document.createElement('ul');
  menu.id = "header_menu";  
    
  var div_subheader = document.getElementById('subheader');
  div_subheader.appendChild(main_title);
  div_subheader.appendChild(menu);

  createElem("li", "Login", "", "", menu.id, loadLogin); 
  createElem("li", "Profile", "", "", menu.id, loadProfile); 
  createElem("li", "Friends", "", "", menu.id, loadFriends); 
}

function loadLogin() {
  var div_tmp_content = document.getElementById('tmp_content');
  div_tmp_content.innerHTML = "";
  
  createElem("span", "Please enter username and password", "", "f_name", 
             "tmp_content", "");
  
  var user_text = createElem("input", "", "user_input", "", "tmp_content", 
                             "");
  user_text.setAttribute("type", "text");
  
  var pass_text = createElem("input", "", "pass_input", "", "tmp_content", 
                             "");
  pass_text.setAttribute("type", "password");
  
  var input_button = createElem("button", "Submit", "", "", "tmp_content", 
                                submitLogin);
  input_button.setAttribute("type", "text");
  
  var input_button = createElem("button", "Cancel", "", "", "tmp_content", 
                                cancelSubmit);
  input_button.setAttribute("type", "text");
}

function loadProfile() {
  pageID = "profile";
  
  clearDiv("local_nav");
  clearDiv("main_content");

  var title = document.createElement('h4');
  title.innerHTML = "Profile";
  
  var menu = document.createElement('ul');
  menu.id = "profile_menu";
    
  var div_local_nav = document.getElementById('local_nav');
  div_local_nav.innerHTML = "";
  div_local_nav.appendChild(title);
  div_local_nav.appendChild(menu);
  
  createElem("li", "Information", "", "", menu.id, showProfileInfo);
  createElem("li", "Update Profile", "", "", menu.id, "");

  showProfileInfo();
}

function showProfileInfo() {
  clearDiv("main_content");
  createTable("Information");

  var dtTable = document.getElementById('data_table');
  
  var new_tr = document.createElement('tr');
  var new_td = document.createElement('td');
  var div_info = document.createElement('div');
  div_info.className = "div_info";  
  dtTable.appendChild(new_tr);
  new_tr.appendChild(new_td);  
  new_td.appendChild(div_info);
  
  var local_user = stateXML.getElementsByTagName('LocalUser')[0];
  
  var name = local_user.getElementsByTagName('Name')[0].textContent;
  var openid = local_user.getElementsByTagName('Uid')[0].textContent;
  var pcid = local_user.getElementsByTagName('PCID')[0].textContent;
  var p2paddress = local_user.getElementsByTagName('Address')[0].textContent;
  var key_fpr = local_user.getElementsByTagName('Fingerprint')[0].textContent;
  var pic = "http://static.ak.fbcdn.net/pics/d_silhouette.gif";
  
  var img_usr = document.createElement('img');
  img_usr.className = "f_img";
  img_usr.setAttribute("src", pic);
  img_usr.setAttribute("width", "200");
  img_usr.setAttribute("height", "126");
  div_info.appendChild(img_usr);
  
  createElem("span", name, "", "fp_name", div_info, "");
  div_info.appendChild(document.createElement('br'));
  div_info.appendChild(document.createElement('br'));
  
  createElem("span", openid, "", "fp_info", div_info, "");
  div_info.appendChild(document.createElement('br'));
  
  createElem("span", pcid, "", "fp_info", div_info, "");
  div_info.appendChild(document.createElement('br'));
  
  createElem("span", key_fpr, "", "fp_info", div_info, "");
  div_info.appendChild(document.createElement('br')); 
}

function loadFriends() {
  pageID = "friends";
  
  clearDiv("local_nav");
  clearDiv("main_content");

  var title = document.createElement('h4');
  title.innerHTML = "Friends";
  
  var menu = document.createElement('ul');
  menu.id = "friends_menu";
    
  var div_local_nav = document.getElementById('local_nav');
  div_local_nav.innerHTML = "";
  div_local_nav.appendChild(title);
  div_local_nav.appendChild(menu);
  
  createElem("li", "All Friends", "", "", menu.id, showAllFriends);
  createElem("li", "Online Friends", "", "", menu.id, showOnlineFriends);
  createElem("li", "Offline Friends", "", "", menu.id, showOfflineFriends);
  createElem("li", "Blocked Friends", "", "", menu.id, showBlockedFriends);
  createElem("li", "Add Friend Uid", "", "", menu.id, addFriendID);
  createElem("li", "Add Certificate", "", "", menu.id, addCertificate);
  
  showFriends();
}

function showAllFriends() {
  option = "All";
  showFriends();
}

function showOnlineFriends() {
  option = "Online";
  showFriends();
}

function showOfflineFriends() {
  option = "Offline";
  showFriends();
}

function showBlockedFriends() {
  option = "Blocked";
  showFriends();
}

function showFriends() {
  clearDiv("main_content");
  createTable(option + " Friends");

  var friends = stateXML.getElementsByTagName('SocialUser');
  
  for (var i = 0; i < friends.length; i++) {
    var friend_time = friends[i].getElementsByTagName('Time')[0].textContent;
    var friend_access = 
      friends[i].getElementsByTagName('Access')[0].textContent;
    if( friend_time == "0" && (option == "All" || option == "Offline") 
       && friend_access == "Allow") {
      friends[i].status = "Status: Offline";
      addFriend(friends[i]);
    }
    else if( friend_time != "0" && (option == "All" || option == "Online") 
      && friend_access == "Allow") {
      friends[i].status = "Status: Online (" + friend_time + ")";
      addFriend(friends[i]);
    }
    else if(friend_access == "Block" && option == "Blocked") {
      friends[i].status = "Status: Blocked";
      addFriend(friends[i]);
    }
  }
}

function addFriend(friend) {
  var address = friend.getElementsByTagName('Address')[0].textContent;
  var dtTable = document.getElementById('data_table');
  
  var new_tr = document.createElement('tr');
  var new_td = document.createElement('td');
  new_td.id = address;
  var div_opts = document.createElement('div');
  div_opts.className = "div_options";
  var div_info = document.createElement('div');
  div_info.className = "div_info";
  
  dtTable.appendChild(new_tr);
  new_tr.appendChild(new_td);  
  new_td.appendChild(div_opts);
  new_td.appendChild(div_info);

  var opt_item = createElem("span", "Unblock", "", "opts_menu", div_opts, 
                            addFriendPost);
  opt_item.address = address;
  div_opts.appendChild(document.createElement('br'));
  
  var opt_item = createElem("span", "Block", "", "opts_menu", div_opts, 
                            removeFriendPost);
  opt_item.address = address;
  div_opts.appendChild(document.createElement('br'));

  var img_usr = document.createElement('img');
  img_usr.className = "f_img";
  img_usr.setAttribute("src", 
                       friend.getElementsByTagName('Pic')[0].textContent);
  img_usr.setAttribute("width", "50");
  img_usr.setAttribute("height", "50");
  div_info.appendChild(img_usr);
  
  var innerHTML = friend.getElementsByTagName('Name')[0].textContent;
  var info_item = createElem("span", innerHTML, "", "f_name", div_info, "");
  div_info.appendChild(document.createElement('br'));
  
  var innerHTML = friend.getElementsByTagName('Alias')[0].textContent + 
    " - " + friend.getElementsByTagName('IP')[0].textContent;
  var info_item = createElem("span", innerHTML, "", "f_info", div_info, "");
  div_info.appendChild(document.createElement('br'));
  
  var innerHTML = friend.status;
  var info_item = createElem("span", innerHTML, "", "f_online", div_info, "");
  div_info.appendChild(document.createElement('br'));
}

function cancelSubmit() {
  var div_tmp_content = document.getElementById('tmp_content');
  div_tmp_content.innerHTML = "";
}

function addFriendID() {  
  var div_tmp_content = document.getElementById('tmp_content');
  div_tmp_content.innerHTML = "";
  
  createElem("span", "Please enter a list of OpenIDs seperated by new line", 
             "", "f_name", "tmp_content", "");
  
  var input_text = createElem("textarea", "", "data_input", "", "tmp_content",
                              "");
  input_text.setAttribute("rows", "10");
  input_text.setAttribute("cols", "50");
  
  var input_button = createElem("button", "Submit", "", "", "tmp_content", 
                                submitFriendID);
  input_button.setAttribute("type", "text");
  
  var input_button = createElem("button", "Cancel", "", "", "tmp_content", 
                                cancelSubmit);
  input_button.setAttribute("type", "text");
}

function submitFriendID() {
  var input_data ="m=add&uids=" + 
    encodeURIComponent(document.getElementById('data_input').value);    
  makeCall(input_data);
  var div_tmp_content = document.getElementById('tmp_content');
  div_tmp_content.innerHTML = "";
}

function addCertificate() {  
  var div_tmp_content = document.getElementById('tmp_content');
  div_tmp_content.innerHTML = "";
  
  createElem("span", "Paste a friend's certificate", 
             "", "f_name", "tmp_content", "");
  
  var input_text = createElem("textarea", "", "data_input", "", "tmp_content",
                              "");
  input_text.setAttribute("rows", "10");
  input_text.setAttribute("cols", "50");
  
  var input_button = createElem("button", "Submit", "", "", "tmp_content", 
                                submitCertificate);
  input_button.setAttribute("type", "text");
  
  var input_button = createElem("button", "Cancel", "", "", "tmp_content", 
                                cancelSubmit);
  input_button.setAttribute("type", "text");
}

function submitCertificate() {
  var input_data ="m=addcert&cert=" + 
    encodeURIComponent(document.getElementById('data_input').value);    
  makeCall(input_data);
  var div_tmp_content = document.getElementById('tmp_content');
  div_tmp_content.innerHTML = "";
}

function submitLogin() {
  var input_data ="m=login&id=drupalbackend&user=" + 
    document.getElementById('user_input').value + "&pass=" +
    document.getElementById('pass_input').value;
  makeCall(input_data);
  var div_tmp_content = document.getElementById('tmp_content');
  div_tmp_content.innerHTML = "";
}

function getConnect() {
  makeCall('m=connect');
}

function getState() {
  makeCall('m=getstate');
}

function removeFriendPost() {
  var postData = "m=update&address=" + 
    this.address + "&access=off";
  makeCall(postData);
}

function addFriendPost() {
  var postData = "m=update&address=" + 
    this.address + "&access=on";
  makeCall(postData);
}

function makeCall(postData) {
  var httpRequest = new XMLHttpRequest();
  httpRequest.overrideMimeType('text/xml');
  httpRequest.onreadystatechange = function() { 
    if(httpRequest.readyState == 4) {
      stateXML = httpRequest.responseXML;
      if(pageID == "friends") {
        loadFriends();
      }
    }
  };  
  httpRequest.open('POST', 'api', true);
  httpRequest.setRequestHeader("Content-type", 
                               "application/x-www-form-urlencoded");
  httpRequest.setRequestHeader("Content-length", postData.length);
  httpRequest.setRequestHeader("Connection", "close");
  httpRequest.send(postData);
}

