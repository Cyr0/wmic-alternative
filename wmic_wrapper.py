#wmic_wrapper.py

#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
WMIC Compatibility Wrapper (wmic.exe replacement)
Translates classic WMIC alias commands to PowerShell Get-CimInstance calls.
"""

import sys
import subprocess

ALIAS_MAP = {
    "bios": "Win32_BIOS",
    "computersystem": "Win32_ComputerSystem",
    "diskdrive": "Win32_DiskDrive",
    "logicaldisk": "Win32_LogicalDisk",
    "os": "Win32_OperatingSystem",
    "process": "Win32_Process",
    "service": "Win32_Service",
    "nicconfig": "Win32_NetworkAdapterConfiguration",
    "baseboard": "Win32_BaseBoard",
    "memorychip": "Win32_PhysicalMemory"
}

def parse_wmic_args(args):
    clean_args = [arg for arg in args if not arg.startswith('/')]
    if not clean_args:
        print("ERROR: No operation specified.", file=sys.stderr)
        sys.exit(1)
        
    alias_key = clean_args[0].lower()
    wmi_class = ALIAS_MAP.get(alias_key)
    
    if not wmi_class:
        print(f"ERROR: Alias '{alias_key}' not supported in compatibility wrapper.", file=sys.stderr)
        sys.exit(1)
        
    rest = clean_args[1:]
    filter_str = ""
    
    if "where" in [r.lower() for r in rest]:
        where_idx = [r.lower() for r in rest].index("where")
        if len(rest) > where_idx + 1:
            filter_str = rest[where_idx + 1]
            rest = rest[:where_idx] + rest[where_idx+2:]
            
    verb = "get"
    properties = []
    if rest:
        verb = rest[0].lower()
        if len(rest) > 1:
            props_raw = " ".join(rest[1:])
            properties = [p.strip() for p in props_raw.replace(",", " ").split() if p.strip()]
            
    return wmi_class, verb, filter_str, properties

def translate_to_powershell(wmi_class, verb, filter_str, properties):
    if verb == "get":
        prop_str = ", ".join([f"'{p}'" for p in properties]) if properties else "*"
        filter_param = f'-Filter "{filter_str}"' if filter_str else ""
        return f"Get-CimInstance -ClassName {wmi_class} {filter_param} | Select-Object {prop_str} | Format-Table -AutoSize"
    elif verb == "list":
        filter_param = f'-Filter "{filter_str}"' if filter_str else ""
        return f"Get-CimInstance -ClassName {wmi_class} {filter_param} | Format-List"
    else:
        return f"Get-CimInstance -ClassName {wmi_class} | Format-Table"

def main():
    if len(sys.argv) < 2:
        print("WMIC Compatibility Wrapper (Python CLI)")
        print("Usage: wmic <alias> [where <condition>] <get/list> [properties]")
        sys.exit(0)
        
    wmi_class, verb, filter_str, properties = parse_wmic_args(sys.argv[1:])
    ps_command = translate_to_powershell(wmi_class, verb, filter_str, properties)
    
    completed = subprocess.run(["powershell.exe", "-NoProfile", "-Command", ps_command], capture_output=True, text=True)
    if completed.stdout:
        print(completed.stdout, end="")
    if completed.stderr:
        print(completed.stderr, file=sys.stderr, end="")
    sys.exit(completed.returncode)

if __name__ == "__main__":
    main()
