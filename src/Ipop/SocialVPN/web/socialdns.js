/*
Copyright (C) 2010 Pierre St Juste <ptony82@ufl.edu>, University of Florida

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

var stateXML = "";
var pageID = "search";
var refresh_time = 0;
var query = null;

init();

function init() {
  loadPage();
  loadHeader();
}

function createElem(itemType, itemHTML, itemID, itemClass, containerName, 
                    functionName) {
  var elem = document.createElement(itemType);
  elem.id = itemID;
  elem.className = itemClass;
  if(functionName != "") {
    if(elem.addEventListener) {
      elem.addEventListener("click", functionName, false);
    }
    else if(elem.attachEvent) {
      elem.attachEvent("onclick", functionName);
    }
  }
  try {
    elem.innerHTML = itemHTML;
  } catch (err) {}
  if(typeof containerName == 'string') {
    var container = document.getElementById(containerName); 
    container.appendChild(elem);
  }
  else {
    containerName.appendChild(elem);
  }
  return elem;
}

function getContent(xmlElem) {
  var content = "";
  if(xmlElem.textContent) {
    content = xmlElem.textContent;
  }
  else if(xmlElem.text) {
    content = xmlElem.text;
  }
  return content;
}

function clearDiv(name) {
  var div_main_content = document.getElementById(name);
  div_main_content.innerHTML = "";
}

function createTable(title) {
  createElem("table", "", "data_table", "", "main_content", "");
  createElem("tr", "", "table_tr", "", "data_table", "");
  createElem("td",title, "", "", "table_tr", "");
  createElem("td","", "", "", "table_tr", "");
}

function createTable2(title) {
  createElem("table", "", "data_table2", "", "side_content", "");
  createElem("tr", "", "table_tr2", "", "data_table2", "");
  createElem("td", title, "", "", "table_tr2", "");
}

function loadPage() {
  document.title = "SocialDNS"; 
  var headElement = document.getElementsByTagName('head')[0];
  var styleSheet = document.createElement('link');
  styleSheet.setAttribute('rel', 'stylesheet');
  styleSheet.setAttribute('type', 'text/css');
  styleSheet.setAttribute('href', 'socialdns.css');
  headElement.appendChild(styleSheet);
  
  document.body = document.createElement('body');
  var div_wrapper = document.createElement('div');
  div_wrapper.id = "wrapper";
  document.body.appendChild(div_wrapper);
  
  createElem("div", "","header","", "wrapper", "");
  createElem("div", "", "subheader","", "header", "");
  createElem("div", "", "main_body", "", "wrapper", "");
  createElem("div", "", "local_nav", "", "main_body", "");
  createElem("div", "", "tmp_content", "", "main_body", "");
  createElem("div", "", "side_content", "", "main_body", "");
  createElem("div", "", "main_content", "", "main_body", "");
}

function loadHeader() {
  var main_title = document.createElement('span');
  main_title.className = "svpn";
  main_title.innerHTML = "SocialDNS Web Management Interface";

  var menu = document.createElement('ul');
  menu.id = "header_menu";

  var div_subheader = document.getElementById('subheader');
  div_subheader.appendChild(main_title);
  div_subheader.appendChild(menu);

  loadSearch();
  getState();
}

function loadMappings() {
  pageID = "search";
  
  clearDiv('local_nav');
  clearDiv('main_content');
  clearDiv('side_content');
  showMappings();
}

function showMappings() {
  clearDiv("main_content");
  createTable("Search Results (Click on mapping to add to your DNS cache)");
  createTable2("Local Mappings");

  var mappinglist = stateXML.getElementsByTagName('Mappings')[0];
  var mappings = mappinglist.getElementsByTagName('DnsMapping');
  var tmappinglist = stateXML.getElementsByTagName('TmpMappings')[0];
  var tmappings = tmappinglist.getElementsByTagName('DnsMapping');
  
  for (var i = 0; i < tmappings.length; i++) {
    addMapping(tmappings[i]);
  }
  for (var i = 0; i < mappings.length; i++) {
    addMapping2(mappings[i]);
  }

  if (tmappings.length == 0) {
    showNoResults();
  }
}

function addMapping(mapping) {
  var dtTable = document.getElementById('data_table');
  var new_tr = document.createElement('tr');
  var new_td = document.createElement('td');
  var new_td2 = document.createElement('td');
  
  dtTable.appendChild(new_tr);
  new_tr.appendChild(new_td);  
  new_tr.appendChild(new_td2);  

  var alias = getContent(mapping.getElementsByTagName('Alias')[0]);
  var ip = getContent(mapping.getElementsByTagName('IP')[0]);
  var address = getContent(mapping.getElementsByTagName('Address')[0]);
  var source = getContent(mapping.getElementsByTagName('Source')[0]);
  var rating = getContent(mapping.getElementsByTagName('Rating')[0]);

  var innerHTML = alias + " - " + ip;
  var info_item = createElem("p", innerHTML, "", "f_name", new_td,
    addOnClick);
  info_item.key = alias + "=" + address + "=" + source + "=" + ip;

  var innerHTML = "Created by " + source;
  var info_item = createElem("p", innerHTML, "", "f_info", new_td, "");
  var rating_item = createElem("span", rating, "", "f_rating", new_td2, "");
}

function addMapping2(mapping) {
  var dtTable = document.getElementById('data_table2');
  var new_tr = document.createElement('tr');
  var new_td = document.createElement('td');
  
  dtTable.appendChild(new_tr);
  new_tr.appendChild(new_td);  

  var innerHTML = getContent(mapping.getElementsByTagName('Alias')[0]);
  var info_item = createElem("span", innerHTML, "", "f_name2", new_td, "");
}

function showNoResults() {
  var dtTable = document.getElementById('data_table');
  var new_tr = document.createElement('tr');
  var new_td = document.createElement('td');
  var new_td2 = document.createElement('td');
  
  dtTable.appendChild(new_tr);
  new_tr.appendChild(new_td);  
  new_tr.appendChild(new_td2);  

  var innerHTML = "<b>No results available.<b>"
  var info_item = createElem("p", innerHTML, "", "", new_td, "");
}

function loadSearch() {  
  var div_tmp_content = document.getElementById('tmp_content');
  div_tmp_content.innerHTML = "";
  
  var id = createElem("input", "", "data_in_id", "", "tmp_content","");

  div_tmp_content.appendChild(document.createElement('br'));

  var in_butt = createElem("button", "Search your friends' cache", "", 
    "", "tmp_content", submitSearch);
  in_butt.setAttribute("type", "text");

  var in_butt2 = createElem("button", "Add mapping to your cache", "", 
    "", "tmp_content", addOnInput);
  in_butt2.setAttribute("type", "text");
}

function submitSearch() {
  query = encodeURIComponent(document.getElementById('data_in_id').value);
  var input_data ="m=sdns.lookup&query=" + query;
  makeCall(input_data, 5000);
}

function addOnInput() {
  var input_data ="m=sdns.addmapping&mapping=" + 
    encodeURIComponent(document.getElementById('data_in_id').value);
  makeCall(input_data, 1000);
}

function addOnClick() {
  var postData = "m=sdns.addmapping&mapping=" +
    encodeURIComponent(getKey(this));
  makeCall(postData, 1000);
}

function getState() {
  if(query != null) {
    makeCall("m=sdns.lookup&query=" + query, 5000);
  }
  else {
    makeCall('m=sdns.getstate', 5000);
  }
}

function getKey(caller) {
  if(caller.key) {
    return caller.key;
  }
  else if(event !== undefined) {
    return event.srcElement.key;
  }
}

function makeCall(postData, ref_time) {
  refresh_time = ref_time;
  var httpRequest;
  if(window.XMLHttpRequest) {
    httpRequest = new XMLHttpRequest();
    //httpRequest.overrideMimeType('text/xml');
  }
  else if(window.ActiveXObject) {
    httpRequest = new ActiveXObject("Microsoft.XMLHTTP");
  }
  else {
    alert("No XMLHTTP support, try another browser");
  }
  
  httpRequest.onreadystatechange = function() { 
    if(httpRequest.readyState == 4) {
      stateXML = httpRequest.responseXML;
      if(refresh_time != 0) {
        window.setTimeout(getState, refresh_time);
      }
      loadMappings();
    }
  };  
  httpRequest.open('POST', 'state.xml', true);
  httpRequest.setRequestHeader("Content-type", 
                               "application/x-www-form-urlencoded");
  httpRequest.setRequestHeader("Content-length", postData.length);
  httpRequest.setRequestHeader("Connection", "close");
  httpRequest.send(postData);
}

