import os.path
from datetime import date

try:
    import xml.etree.ElementTree as ET
except ImportError as e:
    print("Module ElementTree is required")
    quit()

try:
    import requests
except ImportError as e:
    print("Module requests is required")
    quit()
    
XML_URL = 'https://www.shearwater.com/updates/firmwareupdate.xml'
DOWNLOAD_PATH = './'

def create_fw_tuple():
    fws = ()
    XML = requests.get(XML_URL)
    if XML.status_code == 200:
        firmwareupdate = ET.fromstring(XML.content)
        for divecomputer in firmwareupdate:
            for firmware in divecomputer:
                for elem in firmware:
                    if elem.tag == 'url':
                        fws += (elem.text,)
        return fws
    else:
        print("XML could not be found")
        return ()
        
def download_fw(date_str, url):
    filename = url[url.rfind("/")+1:]
    file_path = DOWNLOAD_PATH + date_str + '/' + filename    
    if os.path.isfile(file_path):
        return
    
    binary = requests.get(url, timeout=100)
    if binary.status_code == 200:
        file = open(file_path, 'wb')
        file.write(binary.content)
        file.close()
    else:
        print('failed to download ', url)

def create_dl_dir():
    date_str = str(date.today().day).zfill(2) + str(date.today().month,).zfill(2) +  str(date.today().year)
    try:
        os.mkdir(DOWNLOAD_PATH + date_str)
    except:
        pass
    return date_str

def main():
    fw_no = 0
    fws = create_fw_tuple()
    for url in fws:
        print('Downloading ', url, '\n')
        download_fw(create_dl_dir(), url)
        fw_no += 1
    
    print('Done, ', str(fw_no), ' fws were downloaded')

if __name__ == "__main__":
    main()