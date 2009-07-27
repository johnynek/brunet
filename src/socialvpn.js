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
var refresh_time = 0;
var show_fpr = "no";

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

  //createElem("li", "Friends", "", "", menu.id, loadFriends); 
}

function loadFriends() {
  pageID = "friends";
  
  clearDiv("local_nav");
  clearDiv("main_content");

  var local_user = stateXML.getElementsByTagName('LocalUser')[0];
  var name = local_user.getElementsByTagName('Name')[0].textContent;
  var alias = local_user.getElementsByTagName('Alias')[0].textContent;
  var ip = local_user.getElementsByTagName('IP')[0].textContent;
  var fingerprint = local_user.getElementsByTagName('DhtKey')[0].textContent;
  
  var title = document.createElement('h4');
  title.innerHTML = name + " - " + alias + " - " + ip;

  var menu = document.createElement('ul');
  menu.id = "friends_menu";
    
  var div_local_nav = document.getElementById('local_nav');
  div_local_nav.innerHTML = "";
  div_local_nav.appendChild(title);
  var subtitle = document.createElement('h5');
  subtitle.innerHTML = "Your fingerprint - " + fingerprint;  
  div_local_nav.appendChild(subtitle);
  div_local_nav.appendChild(menu);
  
  createElem("li", "All Friends", "", "", menu.id, showAllFriends);
  createElem("li", "Online Friends", "", "", menu.id, showOnlineFriends);
  createElem("li", "Offline Friends", "", "", menu.id, showOfflineFriends);
  createElem("li", "Blocked Friends", "", "", menu.id, showBlockedFriends);
  createElem("li", "Show/Hide Fingerprints", "", "", menu.id, toggleFprs);
  createElem("li", "Add Friends", "", "", menu.id, addEmails);
  //createElem("li", "Add Fingerprints", "", "", menu.id, addFingerprints);
  
  showFriends();
}

function toggleFprs() {
  if(show_fpr == "yes") {
    show_fpr = "no";
  }
  else {
    show_fpr = "yes";
  }
  loadFriends();
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
    else if(friend_access == "Block" && option == "Blocked" || option == "All") {
      friends[i].status = "Status: Blocked";
      addFriend(friends[i]);
    }
  }
}

function addFriend(friend) {
  var dhtkey = friend.getElementsByTagName('DhtKey')[0].textContent;
  var dtTable = document.getElementById('data_table');
  
  var new_tr = document.createElement('tr');
  var new_td = document.createElement('td');
  new_td.id = dhtkey;
  var div_opts = document.createElement('div');
  div_opts.className = "div_options";
  var div_info = document.createElement('div');
  div_info.className = "div_info";
  
  dtTable.appendChild(new_tr);
  new_tr.appendChild(new_td);  
  new_td.appendChild(div_opts);
  new_td.appendChild(div_info);

  var opt_item = createElem("span", "Block", "", "opts_menu", div_opts, 
                            removeFriendPost);
  opt_item.dhtkey = dhtkey;
  div_opts.appendChild(document.createElement('br'));

  var opt_item = createElem("span", "Unblock", "", "opts_menu", div_opts, 
                            addFriendPost);
  opt_item.dhtkey = dhtkey;
  div_opts.appendChild(document.createElement('br'));
  
  var opt_item = createElem("span", "Delete", "", "opts_menu", div_opts, 
                            deleteFriendPost);
  opt_item.dhtkey = dhtkey;
  div_opts.appendChild(document.createElement('br'));

  var img_usr = document.createElement('img');
  img_usr.className = "f_img";
  img_usr.setAttribute("src", 
                       friend.getElementsByTagName('Pic')[0].textContent);
  img_usr.setAttribute("width", "50");
  img_usr.setAttribute("height", "50");
  div_info.appendChild(img_usr);
  
  var name = friend.getElementsByTagName('Name')[0].textContent;
  var innerHTML = name;
  var info_item = createElem("span", innerHTML, "", "f_name", div_info, "");
  div_info.appendChild(document.createElement('br'));
  
  var innerHTML = friend.getElementsByTagName('Alias')[0].textContent + 
    " - " + friend.getElementsByTagName('IP')[0].textContent;
  var info_item = createElem("span", innerHTML, "", "f_info", div_info, "");
  div_info.appendChild(document.createElement('br'));
  
  var innerHTML = friend.status;
  var info_item = createElem("span", innerHTML, "", "f_online", div_info, "");
  div_info.appendChild(document.createElement('br'));

  if(show_fpr == "yes" ) {  
	var new_tr = document.createElement('tr');
	var new_td = document.createElement('td');
	new_td.id = dhtkey;
	var div_info = document.createElement('div');
	div_info.className = "div_fpr";
	div_info.innerHTML = name + "'s fingerprint - " +
		friend.getElementsByTagName('DhtKey')[0].textContent;
  
	dtTable.appendChild(new_tr);
	new_tr.appendChild(new_td);  
	new_td.appendChild(div_info);
  }
}

function cancelSubmit() {
  var div_tmp_content = document.getElementById('tmp_content');
  div_tmp_content.innerHTML = "";
}

function addEmails() {  
  var div_tmp_content = document.getElementById('tmp_content');
  div_tmp_content.innerHTML = "";
  
  createElem("span", "Enter friend's email addresses (comma seperated)", 
             "", "f_name", "tmp_content", "");
  
  var input_text = createElem("textarea", "", "data_input", "", "tmp_content",
                              "");
  input_text.setAttribute("rows", "5");
  input_text.setAttribute("cols", "50");
  
  var input_button = createElem("button", "Submit", "", "", "tmp_content", 
                                submitEmails);
  input_button.setAttribute("type", "text");
  
  var input_button = createElem("button", "Cancel", "", "", "tmp_content", 
                                cancelSubmit);
  input_button.setAttribute("type", "text");
}

function addFingerprints() {  
  var div_tmp_content = document.getElementById('tmp_content');
  div_tmp_content.innerHTML = "";
  
  createElem("span", "Paste fingerprints (comma seperated)", 
             "", "f_name", "tmp_content", "");
  
  var input_text = createElem("textarea", "", "data_input", "", "tmp_content",
                              "");
  input_text.setAttribute("rows", "5");
  input_text.setAttribute("cols", "50");
  
  var input_button = createElem("button", "Submit", "", "", "tmp_content", 
                                submitFingerprints);
  input_button.setAttribute("type", "text");
  
  var input_button = createElem("button", "Cancel", "", "", "tmp_content", 
                                cancelSubmit);
  input_button.setAttribute("type", "text");
}

function submitEmails() {
  var input_data ="m=add&uids=" + 
    encodeURIComponent(document.getElementById('data_input').value);    
  makeCall(input_data, 2000);
  var div_tmp_content = document.getElementById('tmp_content');
  div_tmp_content.innerHTML = "";
}

function submitFingerprints() {
  var input_data ="m=addfpr&fprs=" + 
    encodeURIComponent(document.getElementById('data_input').value);    
  makeCall(input_data, 2000);
  var div_tmp_content = document.getElementById('tmp_content');
  div_tmp_content.innerHTML = "";
}

function getState() {
  makeCall('m=getstate', 0);
}

function removeFriendPost() {
  var postData = "m=block&fprs=" + 
    encodeURIComponent(this.dhtkey);
  makeCall(postData, 1000);
}

function addFriendPost() {
  var postData = "m=allow&fprs=" + 
    encodeURIComponent(this.dhtkey);
  makeCall(postData, 1000);
}

function deleteFriendPost() {
  var postData = "m=delete&fprs=" + 
    encodeURIComponent(this.dhtkey);
  makeCall(postData, 1000);
}

function makeCall(postData, ref_time) {
  refresh_time = ref_time
  var httpRequest = new XMLHttpRequest();
  httpRequest.overrideMimeType('text/xml');
  httpRequest.onreadystatechange = function() { 
    if(httpRequest.readyState == 4) {
      stateXML = httpRequest.responseXML;
      if(refresh_time != 0) {
        window.setTimeout(getState, refresh_time);
      }
      else if(pageID == "friends") {
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

