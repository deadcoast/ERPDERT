; Preserve the raw user PATH and record ownership only when we add an entry.
[Code]
function RegOpenKeyEx(Key: Integer; SubKey: String; Options, Access: Cardinal;
  var Handle: Integer): Integer;
  external 'RegOpenKeyExW@advapi32.dll stdcall';
function RegQueryValueType(Key: Integer; Name: String; Reserved: Integer;
  var Kind: Cardinal; Data: Integer; var Size: Cardinal): Integer;
  external 'RegQueryValueExW@advapi32.dll stdcall';
function RegCloseKey(Key: Integer): Integer;
  external 'RegCloseKey@advapi32.dll stdcall';
function ExpandEnvironmentStrings(Source, Destination: String; Size: Cardinal): Cardinal;
  external 'ExpandEnvironmentStringsW@kernel32.dll stdcall';

function PathEntryIdentity(Value: String): String;
var Expanded: String; Count: Cardinal;
begin
  Value := Trim(Value);
  if (Length(Value) >= 2) and (Value[1] = '"') and (Value[Length(Value)] = '"') then
    Value := Copy(Value, 2, Length(Value) - 2);
  SetLength(Expanded, 32768);
  Count := ExpandEnvironmentStrings(Value, Expanded, 32768);
  if (Count > 0) and (Count <= 32768) then begin
    SetLength(Expanded, Count - 1);
    Value := Expanded;
  end;
  Result := Uppercase(RemoveBackslashUnlessRoot(Value));
end;

function HasPathEntry(Value, Directory: String): Boolean;
var Entries: TArrayOfString; I: Integer;
begin
  Result := False;
  Entries := StringSplit(Value, [';'], stAll);
  for I := 0 to GetArrayLength(Entries) - 1 do
    if PathEntryIdentity(Entries[I]) = PathEntryIdentity(Directory) then begin
      Result := True;
      Exit;
    end;
end;

function WriteUserPath(Value: String): Boolean;
var Handle, Status: Integer; Kind, Size: Cardinal;
begin
  Result := False;
  Kind := 2; Size := 0;
  Status := RegOpenKeyEx(HKCU, 'Environment', 0, 1, Handle);
  if (Status <> 0) and (Status <> 2) then Exit;
  if Status = 0 then begin
    Status := RegQueryValueType(Handle, '{#PathValue}', 0, Kind, 0, Size);
    RegCloseKey(Handle);
    if (Status <> 0) and (Status <> 2) then Exit;
    if Status = 2 then Kind := 2;
  end;
  if Kind = 1 then Result := RegWriteStringValue(HKCU, 'Environment', '{#PathValue}', Value)
  else if Kind = 2 then Result := RegWriteExpandStringValue(HKCU, 'Environment', '{#PathValue}', Value)
  else Result := False;
end;

function CheckUserPath: String;
var Handle, Status: Integer; Kind, Size: Cardinal;
begin
  Result := '';
  Status := RegOpenKeyEx(HKCU, 'Environment', 0, 3, Handle);
  if Status = 2 then Exit;
  if Status <> 0 then begin
    Result := 'Windows will not let setup change your user command path. Check access to your user environment before retrying.';
    Exit;
  end;
  Kind := 0; Size := 0;
  Status := RegQueryValueType(Handle, '{#PathValue}', 0, Kind, 0, Size);
  RegCloseKey(Handle);
  if ((Status = 0) and (Kind <> 1) and (Kind <> 2)) or ((Status <> 0) and (Status <> 2)) then
    Result := 'Your user command path is not a readable text value. Check that Windows setting before retrying.';
end;

function AddUserPath: String;
var Value, Before, Directory: String; HadValue: Boolean;
begin
  Result := CheckUserPath;
  if Result <> '' then Exit;
  Directory := ExpandConstant('{app}');
  Value := '';
  HadValue := RegQueryStringValue(HKCU, 'Environment', '{#PathValue}', Value);
  if not HadValue and (RegValueExists(HKCU, 'Environment', '{#PathValue}') or (CheckUserPath <> '')) then begin
    Result := 'Windows could not read your command path. Its previous value has been left alone.';
    Exit;
  end;
  if HasPathEntry(Value, Directory) then Exit;
  Before := Value;
  if (Value <> '') and (Value[Length(Value)] <> ';') then Value := Value + ';';
  { Record intent before mutation. An interrupted intent with no matching PATH
    entry cannot make removal delete another entry. }
  if not (RegWriteStringValue(HKCU, '{#StateKey}', 'AddedPath', Directory) and
    RegWriteStringValue(HKCU, '{#StateKey}', 'PathBefore', Before) and
    RegWriteStringValue(HKCU, '{#StateKey}', 'PathAfter', Value + Directory)) then begin
    Result := 'Windows could not record the command-path change; PATH was left unchanged.';
    Exit;
  end;
  if HadValue then Directory := 'yes' else Directory := 'no';
  if not RegWriteStringValue(HKCU, '{#StateKey}', 'PathExisted', Directory) then begin
    Result := 'Windows could not record the command-path change; PATH was left unchanged.';
    Exit;
  end;
  if not WriteUserPath(Value + ExpandConstant('{app}')) then
    Result := 'Windows could not add DERT to your command path. Re-run setup after checking access to your user environment.';
end;

procedure ForgetPathOwnership;
begin
  RegDeleteValue(HKCU, '{#StateKey}', 'AddedPath');
  RegDeleteValue(HKCU, '{#StateKey}', 'PathBefore');
  RegDeleteValue(HKCU, '{#StateKey}', 'PathAfter');
  RegDeleteValue(HKCU, '{#StateKey}', 'PathExisted');
end;

function RemoveUserPath: Boolean;
var Value, Directory, Updated, Before, After, Existed: String;
  Entries: TArrayOfString; I, Kept: Integer; Removed: Boolean;
begin
  Result := True;
  if not RegQueryStringValue(HKCU, '{#StateKey}', 'AddedPath', Directory) then Exit;
  if CheckUserPath <> '' then begin Result := False; Exit; end;
  if not RegQueryStringValue(HKCU, 'Environment', '{#PathValue}', Value) then begin
    Result := not RegValueExists(HKCU, 'Environment', '{#PathValue}') and (CheckUserPath = '');
    Exit;
  end;
  if RegQueryStringValue(HKCU, '{#StateKey}', 'PathAfter', After) and (Value = After) and
    RegQueryStringValue(HKCU, '{#StateKey}', 'PathBefore', Before) then begin
    RegQueryStringValue(HKCU, '{#StateKey}', 'PathExisted', Existed);
    if Existed = 'no' then Result := RegDeleteValue(HKCU, 'Environment', '{#PathValue}')
    else Result := WriteUserPath(Before);
    Exit;
  end;
  Entries := StringSplit(Value, [';'], stAll);
  Updated := ''; Kept := 0; Removed := False;
  for I := 0 to GetArrayLength(Entries) - 1 do begin
    if not Removed and (PathEntryIdentity(Entries[I]) = PathEntryIdentity(Directory)) then
      Removed := True
    else begin
      if Kept > 0 then Updated := Updated + ';';
      Updated := Updated + Entries[I];
      Kept := Kept + 1;
    end;
  end;
  if Updated <> Value then Result := WriteUserPath(Updated);
end;
