#include <windows.h>
#include <stdio.h>
#include <winioctl.h>
#include <setupapi.h>
#include "windows_tap.h"

typedef struct {
  HANDLE hand;
  OVERLAPPED read, write;
} windows_tap;

/* Returns a GUID given a network device name */

char * network_device_name_to_guid(char * name) {
  int count, i_0, i_1, i_2;
  DWORD size;
  HKEY key_0, key_1, key_2;
  char name_0[255], name_1[255], name_2[255], full_path[1024];
  char * return_value;

  /* Open up Networking in the registry */
  RegOpenKeyEx(HKEY_LOCAL_MACHINE, NETWORK_PATH, 0, KEY_READ, &key_0);
    
  for(i_0 = 0; ; i_0++) {
    size = 255;
    /* Enumerate through the different keys under Network\layer0 */
    if(RegEnumKeyEx(key_0, i_0, name_0, &size, NULL, NULL, NULL, NULL) 
      != ERROR_SUCCESS) {
      i_0 = -1;
      break;
    }
    sprintf(full_path, "%s\\%s", NETWORK_PATH, name_0);
    /* Open the current key we're enumerating through Network\layer0 */
    RegOpenKeyEx(HKEY_LOCAL_MACHINE, full_path, 0, KEY_READ, &key_1); 
    for(i_1 = 0; ; i_1++) {
      size = 255;
      /* This enumerates through the next layer Network\layer0\layer1 */
      if(RegEnumKeyEx(key_1, i_1, name_1, &size, NULL, NULL, NULL, NULL) 
        != ERROR_SUCCESS) {
        i_1 = -1;
        break;
      }

      sprintf(full_path, "%s\\%s\\%s\\Connection", NETWORK_PATH, 
        name_0, name_1);
      /* This opens keys that we're looking for, if they don't exist, let's 
         continue */
      if(RegOpenKeyEx(HKEY_LOCAL_MACHINE, full_path, 0, KEY_READ, &key_2) 
        != ERROR_SUCCESS) {
        continue;
      }
      size = 255;
      /* We get the Name of the network interface, if it matches, let's get the
         GUID and return */
      RegQueryValueEx(key_2, "Name", 0, NULL, name_2, &size);
      if(!strcmp(name, name_2)) {
        RegCloseKey(key_0);
        RegCloseKey(key_1);
        RegCloseKey(key_2);
      /* We have to create a new copy in global heap! */
        return_value = (char *) malloc(strlen(name_1) * sizeof(char));
        strcpy(return_value, name_1);
        return return_value;
      }
      RegCloseKey(key_2);
    }
    RegCloseKey(key_1);
  }
  RegCloseKey(key_0);
  return NULL;
}


int read_tap(windows_tap * fd, char * data, int len) {
  int read;
  /* ReadFile is asynchronous and GetOverLappedResult is blocking, we have to do
     this cause Windows makes things painful when deal with asynchronous I/O */
  ReadFile(fd->hand, data, len, (LPDWORD) &read, &(fd->read));
  GetOverlappedResult(fd->hand, &fd->read, (LPDWORD) &read, TRUE);
  return read;
}

int send_tap(windows_tap * fd, char * data, int len) {
  int written;
  /* WriteFile is asynchronous and GetOverLappedResult is blocking, we have to 
     do this cause Windows makes things painful when deal with 
     asynchronous I/O */
  WriteFile(fd->hand, data, len, (LPDWORD) &written, &(fd->write));
  GetOverlappedResult(fd->hand, &fd->write, (LPDWORD) &written, TRUE);
  return written;
}

windows_tap * open_tap(char *device_name) {
  int len, result = 0, status = 1, count;
  windows_tap * fd = (windows_tap *) malloc(sizeof(windows_tap));
  char device_path[255], *device_guid;
  /* Get our device guid */
  device_guid = network_device_name_to_guid(device_name);
  if(device_guid == NULL) {
    return (windows_tap *) -1;
  }
  sprintf(device_path, "%s%s%s", USERMODEDEVICEDIR, device_guid, TAPSUFFIX);
  /* This gets us Handle (pointer) to operate on the tap device */
  fd->hand = CreateFile (device_path, GENERIC_READ | GENERIC_WRITE, 0, 0,
    OPEN_EXISTING, FILE_ATTRIBUTE_SYSTEM | FILE_FLAG_OVERLAPPED, 0);
    
  if(fd->hand != INVALID_HANDLE_VALUE) {
    /* This turns "connects" the tap device */
    if(!DeviceIoControl(fd->hand, TAP_IOCTL_SET_MEDIA_STATUS, &status, 
      sizeof (status), &status, sizeof (status), (LPDWORD) &len, NULL)) {
      free(fd);
      fd = (windows_tap *) -1;
    }
    else {
      /* We do this once so we don't have to redo this every time a read or 
         write occurs! */
      fd->read.hEvent = CreateEvent(NULL, TRUE, TRUE, NULL);
      fd->read.Offset = 0;
      fd->read.OffsetHigh = 0;
      fd->write.hEvent = CreateEvent(NULL, TRUE, TRUE, NULL);
      fd->write.Offset = 0;
      fd->write.OffsetHigh = 0;
    }
  }
  else {
    fd = (windows_tap *) -1;
  }
  
  return fd;
}

int close_tap(windows_tap* device) {
  return CloseHandle(device->hand);
}

