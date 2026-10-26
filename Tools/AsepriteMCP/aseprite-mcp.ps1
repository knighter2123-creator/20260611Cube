# Aseprite MCP server (stdio, JSON-RPC 2.0) for Claude Code.
# Runs Aseprite in --batch mode with generated Lua scripts. No dependencies beyond Windows PowerShell 5.1.
#
# Env:
#   ASEPRITE_PATH         Aseprite.exe location (auto-detected when unset)
#   ASEPRITE_MCP_BASEDIR  base directory for relative paths (defaults to the launch directory)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$Utf8 = New-Object System.Text.UTF8Encoding $false
$reader = New-Object System.IO.StreamReader([Console]::OpenStandardInput(), $Utf8)
$writer = New-Object System.IO.StreamWriter([Console]::OpenStandardOutput(), $Utf8)
$writer.AutoFlush = $true
$writer.NewLine = "`n"

$script:BaseDir = if ($env:ASEPRITE_MCP_BASEDIR) { $env:ASEPRITE_MCP_BASEDIR } else { (Get-Location).Path }
$script:TempDir = Join-Path ([IO.Path]::GetTempPath()) 'aseprite-mcp'
[void](New-Item -ItemType Directory -Force $script:TempDir)

function Find-Aseprite {
    if ($env:ASEPRITE_PATH -and (Test-Path $env:ASEPRITE_PATH)) { return $env:ASEPRITE_PATH }
    $candidates = @(
        "${env:ProgramFiles(x86)}\Steam\steamapps\common\Aseprite\Aseprite.exe",
        "$env:ProgramFiles\Steam\steamapps\common\Aseprite\Aseprite.exe",
        "$env:ProgramFiles\Aseprite\Aseprite.exe",
        "${env:ProgramFiles(x86)}\Aseprite\Aseprite.exe",
        "$env:LOCALAPPDATA\Programs\Aseprite\Aseprite.exe"
    )
    foreach ($c in $candidates) { if (Test-Path $c) { return $c } }
    return $null
}
$script:AsepriteExe = Find-Aseprite

# ---------------------------------------------------------------- helpers

function Send-Message($obj) {
    $writer.WriteLine((ConvertTo-Json -InputObject $obj -Depth 30 -Compress))
}

function New-TextResult([string]$text, [bool]$isError = $false) {
    @{ content = @(@{ type = 'text'; text = $text }); isError = $isError }
}

function Get-Arg($a, [string]$name, $default = $null) {
    if ($null -ne $a -and $null -ne $a.PSObject.Properties[$name] -and $null -ne $a.$name) { return $a.$name }
    return $default
}

function Resolve-UserPath([string]$p) {
    if ([string]::IsNullOrWhiteSpace($p)) { throw 'path is required' }
    if (-not [IO.Path]::IsPathRooted($p)) { $p = Join-Path $script:BaseDir $p }
    return [IO.Path]::GetFullPath($p)
}

function Assert-ParentDir([string]$p) {
    $dir = Split-Path -Parent $p
    if ($dir -and -not (Test-Path $dir)) { [void](New-Item -ItemType Directory -Force $dir) }
}

function Format-Arg([string]$s) { '"' + ($s -replace '"', '\"') + '"' }

function ConvertTo-Lua($v) {
    if ($null -eq $v) { return 'nil' }
    if ($v -is [bool]) { if ($v) { return 'true' } else { return 'false' } }
    if ($v -is [string]) {
        $e = $v.Replace('\', '\\').Replace('"', '\"').Replace("`r", '\r').Replace("`n", '\n')
        return '"' + $e + '"'
    }
    if ($v -is [int] -or $v -is [long] -or $v -is [double] -or $v -is [decimal] -or $v -is [single] -or $v -is [int16] -or $v -is [byte]) {
        return [string]::Format([Globalization.CultureInfo]::InvariantCulture, '{0}', $v)
    }
    if ($v -is [System.Collections.IDictionary]) {
        $parts = foreach ($k in $v.Keys) { '[' + (ConvertTo-Lua ([string]$k)) + ']=' + (ConvertTo-Lua $v[$k]) }
        return '{' + ($parts -join ',') + '}'
    }
    if ($v -is [System.Management.Automation.PSCustomObject]) {
        $parts = foreach ($p in $v.PSObject.Properties) { '[' + (ConvertTo-Lua $p.Name) + ']=' + (ConvertTo-Lua $p.Value) }
        return '{' + ($parts -join ',') + '}'
    }
    if ($v -is [System.Collections.IEnumerable]) {
        $parts = foreach ($item in $v) { ConvertTo-Lua $item }
        return '{' + ($parts -join ',') + '}'
    }
    return ConvertTo-Lua ([string]$v)
}

# "#RRGGBB", "#RRGGBBAA", "transparent", or a palette index -> @{r;g;b;a} / @{index}
function ConvertTo-ColorSpec($c) {
    if ($null -eq $c) { throw 'color is required' }
    if ($c -is [string]) {
        $s = $c.Trim()
        if ($s -eq 'transparent') { return @{ r = 0; g = 0; b = 0; a = 0 } }
        if ($s -match '^\d+$') { return @{ index = [int]$s } }
        if ($s -match '^#?([0-9a-fA-F])([0-9a-fA-F])([0-9a-fA-F])$') { $s = '#' + $Matches[1] * 2 + $Matches[2] * 2 + $Matches[3] * 2 }
        if ($s -match '^#?([0-9a-fA-F]{6})([0-9a-fA-F]{2})?$') {
            $hex = $Matches[1]
            $alpha = 255
            if ($Matches[2]) { $alpha = [Convert]::ToInt32($Matches[2], 16) }
            return @{
                r = [Convert]::ToInt32($hex.Substring(0, 2), 16)
                g = [Convert]::ToInt32($hex.Substring(2, 2), 16)
                b = [Convert]::ToInt32($hex.Substring(4, 2), 16)
                a = $alpha
            }
        }
        throw "invalid color '$c' (use #RRGGBB, #RRGGBBAA, transparent, or a palette index)"
    }
    return @{ index = [int]$c }
}

function Invoke-Aseprite([string]$arguments, [int]$timeoutSec = 120) {
    if (-not $script:AsepriteExe) { throw 'Aseprite.exe not found. Set ASEPRITE_PATH in the MCP server env.' }
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $script:AsepriteExe
    $psi.Arguments = $arguments
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.RedirectStandardInput = $true
    $psi.CreateNoWindow = $true
    $psi.StandardOutputEncoding = $Utf8
    $psi.StandardErrorEncoding = $Utf8
    $p = [System.Diagnostics.Process]::Start($psi)
    $p.StandardInput.Close()
    $outTask = $p.StandardOutput.ReadToEndAsync()
    $errTask = $p.StandardError.ReadToEndAsync()
    if (-not $p.WaitForExit($timeoutSec * 1000)) {
        try { $p.Kill() } catch {}
        throw "Aseprite timed out after $timeoutSec s"
    }
    $p.WaitForExit()
    return @{ exit = $p.ExitCode; out = $outTask.Result; err = $errTask.Result }
}

$LuaPrelude = @'
local ARR = {}
local function arr(t) return setmetatable(t or {}, ARR) end
local function J(v)
  local t = type(v)
  if t == "nil" then return "null" end
  if t == "boolean" then return v and "true" or "false" end
  if t == "number" then
    if v ~= v or v == math.huge or v == -math.huge then return "null" end
    if math.type(v) == "integer" then return tostring(v) end
    return string.format("%.10g", v)
  end
  if t == "string" then
    return '"' .. (v:gsub('[%c"\\]', function(c)
      if c == '"' then return '\\"' elseif c == '\\' then return '\\\\'
      elseif c == '\n' then return '\\n' elseif c == '\r' then return '\\r' elseif c == '\t' then return '\\t' end
      return string.format('\\u%04x', c:byte())
    end)) .. '"'
  end
  if t == "table" then
    local out = {}
    if getmetatable(v) == ARR or #v > 0 then
      for i = 1, #v do out[i] = J(v[i]) end
      return "[" .. table.concat(out, ",") .. "]"
    end
    for k, val in pairs(v) do out[#out + 1] = J(tostring(k)) .. ":" .. J(val) end
    return "{" .. table.concat(out, ",") .. "}"
  end
  return J(tostring(v))
end
local function emit(v) print("@@RESULT@@" .. J(v)) end
local function openSprite(path)
  local spr = app.open(path)
  if not spr then error("cannot open sprite: " .. path) end
  return spr
end
local function getFrame(spr, fi)
  fi = fi or 1
  if fi < 1 or fi > #spr.frames then error("frame " .. fi .. " out of range (1-" .. #spr.frames .. ")") end
  return spr.frames[fi], fi
end
local function findLayer(spr, name)
  local found
  local function walk(list, parent)
    for _, l in ipairs(list) do
      local path = parent and (parent .. "/" .. l.name) or l.name
      if not found and not l.isGroup and (l.name == name or path == name) then found = l end
      if l.isGroup then walk(l.layers, path) end
    end
  end
  walk(spr.layers, nil)
  return found
end
local function setCelImage(spr, layer, frame, img)
  local cel = layer:cel(frame)
  if cel then cel.image = img; cel.position = Point(0, 0)
  else spr:newCel(layer, frame, img, Point(0, 0)) end
end
-- c = {r,g,b,a} or {index}; returns a pixel value for the sprite's color mode
local function pix(spr, c)
  local mode = spr.colorMode
  local pal = spr.palettes[1]
  if c.index then
    if mode == ColorMode.INDEXED then return c.index end
    local col = pal:getColor(c.index)
    c = { r = col.red, g = col.green, b = col.blue, a = col.alpha }
  end
  if mode == ColorMode.RGB then return app.pixelColor.rgba(c.r, c.g, c.b, c.a) end
  if mode == ColorMode.GRAY then
    return app.pixelColor.graya(math.floor(0.299 * c.r + 0.587 * c.g + 0.114 * c.b + 0.5), c.a)
  end
  local tr = spr.transparentColor
  if c.a == 0 then return tr end
  local best, bestD = nil, math.huge
  for i = 0, #pal - 1 do
    if i ~= tr then
      local p = pal:getColor(i)
      local d = (p.red - c.r) ^ 2 + (p.green - c.g) ^ 2 + (p.blue - c.b) ^ 2
      if d < bestD then best, bestD = i, d end
    end
  end
  return best or tr
end
'@

# Runs a tool body wrapped in pcall; returns the JSON string passed to emit().
function Invoke-LuaTool([string]$body, $params, [string]$spritePath = $null, [int]$timeoutSec = 120) {
    $lua = $LuaPrelude + "`nlocal P = " + (ConvertTo-Lua $params) + "`n" +
        "local __ok, __err = pcall(function()`n" + $body + "`nend)`n" +
        "if not __ok then print('@@ERROR@@' .. tostring(__err)) end`n"
    $file = Join-Path $script:TempDir ([guid]::NewGuid().ToString('N') + '.lua')
    [IO.File]::WriteAllText($file, $lua, $Utf8)
    try {
        $argList = '--batch '
        if ($spritePath) { $argList += (Format-Arg $spritePath) + ' ' }
        $argList += '--script ' + (Format-Arg $file)
        $r = Invoke-Aseprite $argList $timeoutSec
    } finally {
        Remove-Item $file -ErrorAction SilentlyContinue
    }
    $out = [string]$r.out
    $errIdx = $out.IndexOf('@@ERROR@@')
    if ($errIdx -ge 0) { throw ('Lua error: ' + $out.Substring($errIdx + 9).Trim()) }
    $resIdx = $out.IndexOf('@@RESULT@@')
    if ($resIdx -lt 0) { throw "Aseprite returned no result (exit $($r.exit)).`nstdout: $out`nstderr: $($r.err)" }
    $json = $out.Substring($resIdx + 10)
    $nl = $json.IndexOf("`n")
    if ($nl -ge 0) { $json = $json.Substring(0, $nl) }
    return $json.Trim()
}

function Get-NewFiles([string]$outputPath, [datetime]$since) {
    $dir = Split-Path -Parent $outputPath
    if (-not (Test-Path $dir)) { return @() }
    $files = Get-ChildItem -LiteralPath $dir -File | Where-Object { $_.LastWriteTime -ge $since } | Select-Object -First 100
    return @($files | ForEach-Object { $_.FullName })
}

# ---------------------------------------------------------------- tools

$ColorProp = @{ type = @('string', 'integer'); description = '"#RGB", "#RRGGBB", "#RRGGBBAA", "transparent", or a palette index' }

$Tools = @(
    @{
        name = 'aseprite_get_info'
        description = 'Read an .aseprite/.ase/.png file: size, color mode, palette size, frames (durations), layers (top to bottom, with group paths), tags and slices.'
        inputSchema = @{ type = 'object'; properties = @{ path = @{ type = 'string'; description = 'Sprite file path (absolute or relative to the project)' } }; required = @('path') }
    },
    @{
        name = 'aseprite_create_sprite'
        description = 'Create a new sprite file and save it.'
        inputSchema = @{
            type = 'object'
            properties = @{
                path = @{ type = 'string'; description = 'Output path (.aseprite recommended)' }
                width = @{ type = 'integer'; minimum = 1 }
                height = @{ type = 'integer'; minimum = 1 }
                color_mode = @{ type = 'string'; enum = @('rgb', 'grayscale', 'indexed'); description = 'Default rgb' }
                frames = @{ type = 'integer'; minimum = 1; description = 'Number of frames (default 1)' }
                background = $ColorProp
                palette = @{ type = 'string'; description = 'Built-in palette: DB32, DB16, PICO-8 (default DB32 for indexed, none otherwise)' }
                overwrite = @{ type = 'boolean'; description = 'Allow replacing an existing file (default false)' }
            }
            required = @('path', 'width', 'height')
        }
    },
    @{
        name = 'aseprite_draw_pixels'
        description = 'Set individual pixels and/or fill rectangles on one layer/frame of a sprite, then save it. Pixels are written directly (no blending). Missing layer names are created.'
        inputSchema = @{
            type = 'object'
            properties = @{
                path = @{ type = 'string' }
                layer = @{ type = 'string'; description = 'Layer name or group path like "Body/Arm". Default: top-most top-level image layer' }
                frame = @{ type = 'integer'; minimum = 1; description = '1-based frame number (default 1)' }
                pixels = @{
                    type = 'array'
                    items = @{ type = 'object'; properties = @{ x = @{ type = 'integer' }; y = @{ type = 'integer' }; color = $ColorProp }; required = @('x', 'y', 'color') }
                }
                rects = @{
                    type = 'array'
                    description = 'Filled rectangles, applied before pixels'
                    items = @{ type = 'object'; properties = @{ x = @{ type = 'integer' }; y = @{ type = 'integer' }; w = @{ type = 'integer' }; h = @{ type = 'integer' }; color = $ColorProp }; required = @('x', 'y', 'w', 'h', 'color') }
                }
            }
            required = @('path')
        }
    },
    @{
        name = 'aseprite_preview'
        description = 'Render one frame of a sprite (all visible layers) as a PNG image so you can look at it. Pixel-perfect upscale; transparent areas shown on a checkerboard.'
        inputSchema = @{
            type = 'object'
            properties = @{
                path = @{ type = 'string' }
                frame = @{ type = 'integer'; minimum = 1; description = '1-based frame (default 1)' }
                scale = @{ type = 'integer'; minimum = 1; maximum = 32; description = 'Integer upscale. Default: fit to max_size' }
                max_size = @{ type = 'integer'; description = 'Target max dimension for automatic scale (default 512)' }
                checker = @{ type = 'boolean'; description = 'Checkerboard behind transparency (default true)' }
            }
            required = @('path')
        }
    },
    @{
        name = 'aseprite_export'
        description = 'Export a sprite to an image file (png/gif/etc.) via the Aseprite CLI. Put {frame} or {tag} in output to write one file per frame/tag; exporting multiple frames to a .gif makes an animation.'
        inputSchema = @{
            type = 'object'
            properties = @{
                path = @{ type = 'string' }
                output = @{ type = 'string'; description = 'Output file, e.g. Assets/Sprites/hero.png or out/hero_{frame}.png' }
                scale = @{ type = 'number'; description = 'Resize factor (default 1)' }
                layer = @{ type = 'string'; description = 'Export only this layer' }
                tag = @{ type = 'string'; description = 'Export only frames of this tag' }
                frame_from = @{ type = 'integer'; description = '0-based first frame (CLI convention)' }
                frame_to = @{ type = 'integer'; description = '0-based last frame (CLI convention)' }
            }
            required = @('path', 'output')
        }
    },
    @{
        name = 'aseprite_export_sheet'
        description = 'Export a sprite sheet PNG (and optional JSON frame data) via the Aseprite CLI.'
        inputSchema = @{
            type = 'object'
            properties = @{
                path = @{ type = 'string' }
                sheet = @{ type = 'string'; description = 'Output sheet image path' }
                data = @{ type = 'string'; description = 'Optional JSON data output path' }
                sheet_type = @{ type = 'string'; enum = @('horizontal', 'vertical', 'rows', 'columns', 'packed'); description = 'Default horizontal' }
                columns = @{ type = 'integer'; description = 'Fixed column count (for rows type)' }
                scale = @{ type = 'number' }
                tag = @{ type = 'string' }
                layer = @{ type = 'string' }
                split_layers = @{ type = 'boolean' }
                split_tags = @{ type = 'boolean' }
                padding = @{ type = 'integer'; description = 'Shape padding in pixels' }
                trim = @{ type = 'boolean' }
            }
            required = @('path', 'sheet')
        }
    },
    @{
        name = 'aseprite_run_lua'
        description = 'Run arbitrary Aseprite Lua (API: https://www.aseprite.org/api) in batch mode and return stdout. If sprite_path is given it is opened as app.activeSprite. Changes are NOT saved unless the script calls sprite:saveAs(...). Helpers available: J(value) -> JSON string, openSprite(path), findLayer(sprite, name).'
        inputSchema = @{
            type = 'object'
            properties = @{
                code = @{ type = 'string'; description = 'Lua source; use print() for output' }
                sprite_path = @{ type = 'string' }
                timeout_sec = @{ type = 'integer'; description = 'Default 120' }
            }
            required = @('code')
        }
    }
)

function Invoke-Tool([string]$name, $a) {
    switch ($name) {
        'aseprite_get_info' {
            $body = @'
local spr = openSprite(P.path)
local modes = { [ColorMode.RGB] = "rgb", [ColorMode.GRAY] = "grayscale", [ColorMode.INDEXED] = "indexed" }
local dirs = { [AniDir.FORWARD] = "forward", [AniDir.REVERSE] = "reverse", [AniDir.PING_PONG] = "pingpong" }
local frames = arr()
for i, f in ipairs(spr.frames) do frames[#frames + 1] = { index = i, duration_ms = math.floor(f.duration * 1000 + 0.5) } end
local layers = arr()
local function walk(list, depth, parent)
  for i = #list, 1, -1 do
    local l = list[i]
    local path = parent and (parent .. "/" .. l.name) or l.name
    layers[#layers + 1] = { name = l.name, path = path, depth = depth, group = l.isGroup, visible = l.isVisible,
      background = l.isBackground, opacity = l.opacity, cels = (not l.isGroup) and #l.cels or nil }
    if l.isGroup then walk(l.layers, depth + 1, path) end
  end
end
walk(spr.layers, 0, nil)
local tags = arr()
for _, t in ipairs(spr.tags) do
  tags[#tags + 1] = { name = t.name, from = t.fromFrame.frameNumber, to = t.toFrame.frameNumber, direction = dirs[t.aniDir] or tostring(t.aniDir) }
end
local slices = arr()
for _, s in ipairs(spr.slices) do
  slices[#slices + 1] = { name = s.name, x = s.bounds.x, y = s.bounds.y, w = s.bounds.width, h = s.bounds.height }
end
emit({ path = spr.filename, width = spr.width, height = spr.height, color_mode = modes[spr.colorMode] or tostring(spr.colorMode),
  palette_size = #spr.palettes[1], frames = frames, layers_top_to_bottom = layers, tags = tags, slices = slices })
'@
            $path = Resolve-UserPath (Get-Arg $a 'path')
            if (-not (Test-Path -LiteralPath $path)) { throw "file not found: $path" }
            return New-TextResult (Invoke-LuaTool $body @{ path = $path })
        }
        'aseprite_create_sprite' {
            $body = @'
local modes = { rgb = ColorMode.RGB, grayscale = ColorMode.GRAY, indexed = ColorMode.INDEXED }
local mode = modes[P.color_mode] or error("invalid color_mode: " .. tostring(P.color_mode))
local spr = Sprite(P.width, P.height, mode)
if P.palette then spr:setPalette(Palette{ fromResource = P.palette }) end
for _ = 2, P.frames do spr:newEmptyFrame() end
if P.background then
  for _, f in ipairs(spr.frames) do
    local img = Image(spr.spec)
    img:clear(pix(spr, P.background))
    setCelImage(spr, spr.layers[1], f, img)
  end
end
spr:saveAs(P.path)
emit({ path = P.path, width = spr.width, height = spr.height, frames = #spr.frames })
'@
            $path = Resolve-UserPath (Get-Arg $a 'path')
            if ((Test-Path -LiteralPath $path) -and -not (Get-Arg $a 'overwrite' $false)) { throw "file already exists: $path (pass overwrite=true to replace)" }
            Assert-ParentDir $path
            $bg = Get-Arg $a 'background'
            $mode = [string](Get-Arg $a 'color_mode' 'rgb')
            $palette = Get-Arg $a 'palette'
            if (-not $palette -and $mode -eq 'indexed') { $palette = 'DB32' }
            $params = @{
                path = $path
                width = [int](Get-Arg $a 'width')
                height = [int](Get-Arg $a 'height')
                color_mode = $mode
                palette = $palette
                frames = [int](Get-Arg $a 'frames' 1)
                background = $(if ($null -ne $bg) { ConvertTo-ColorSpec $bg } else { $null })
            }
            return New-TextResult (Invoke-LuaTool $body $params)
        }
        'aseprite_draw_pixels' {
            $body = @'
local spr = openSprite(P.path)
local layer, created
if P.layer then
  layer = findLayer(spr, P.layer)
  if not layer then layer = spr:newLayer(); layer.name = P.layer; created = true end
else
  for i = #spr.layers, 1, -1 do
    if not spr.layers[i].isGroup then layer = spr.layers[i]; break end
  end
  if not layer then error("no top-level image layer; pass layer") end
end
local frame, fi = getFrame(spr, P.frame)
local img = Image(spr.spec)
local cel = layer:cel(frame)
if cel then img:drawImage(cel.image, cel.position) end
local W, H = spr.width, spr.height
local drawn, skipped = 0, 0
for _, r in ipairs(P.rects) do
  local v = pix(spr, r.c)
  for y = math.max(0, r.y), math.min(H - 1, r.y + r.h - 1) do
    for x = math.max(0, r.x), math.min(W - 1, r.x + r.w - 1) do img:drawPixel(x, y, v); drawn = drawn + 1 end
  end
end
for _, p in ipairs(P.pixels) do
  if p.x >= 0 and p.y >= 0 and p.x < W and p.y < H then
    img:drawPixel(p.x, p.y, pix(spr, p.c)); drawn = drawn + 1
  else
    skipped = skipped + 1
  end
end
setCelImage(spr, layer, frame, img)
spr:saveAs(spr.filename)
emit({ pixels_written = drawn, skipped_out_of_bounds = skipped, layer = layer.name, frame = fi, created_layer = created or false })
'@
            $path = Resolve-UserPath (Get-Arg $a 'path')
            if (-not (Test-Path -LiteralPath $path)) { throw "file not found: $path" }
            $pixels = @(foreach ($p in @(Get-Arg $a 'pixels' @())) { @{ x = [int]$p.x; y = [int]$p.y; c = (ConvertTo-ColorSpec $p.color) } })
            $rects = @(foreach ($r in @(Get-Arg $a 'rects' @())) { @{ x = [int]$r.x; y = [int]$r.y; w = [int]$r.w; h = [int]$r.h; c = (ConvertTo-ColorSpec $r.color) } })
            if ($pixels.Count -eq 0 -and $rects.Count -eq 0) { throw 'pass pixels and/or rects' }
            $params = @{ path = $path; layer = (Get-Arg $a 'layer'); frame = [int](Get-Arg $a 'frame' 1); pixels = $pixels; rects = $rects }
            return New-TextResult (Invoke-LuaTool $body $params)
        }
        'aseprite_preview' {
            $body = @'
local spr = openSprite(P.path)
local _, fi = getFrame(spr, P.frame)
local w, h = spr.width, spr.height
local src = Image(w, h, ColorMode.RGB)
src:drawSprite(spr, fi)
local s = P.scale or math.max(1, math.min(32, math.floor(P.max_size / math.max(w, h))))
local out = Image(w * s, h * s, ColorMode.RGB)
local pc = app.pixelColor
for y = 0, h - 1 do
  for x = 0, w - 1 do
    local v = src:getPixel(x, y)
    local r, g, b, a = pc.rgbaR(v), pc.rgbaG(v), pc.rgbaB(v), pc.rgbaA(v)
    for dy = 0, s - 1 do
      for dx = 0, s - 1 do
        local ox, oy = x * s + dx, y * s + dy
        local o = v
        if P.checker and a < 255 then
          local bg = ((ox // 8 + oy // 8) % 2 == 0) and 204 or 255
          local ia = 255 - a
          o = pc.rgba((r * a + bg * ia) // 255, (g * a + bg * ia) // 255, (b * a + bg * ia) // 255, 255)
        end
        out:drawPixel(ox, oy, o)
      end
    end
  end
end
out:saveAs(P.out)
emit({ width = w, height = h, scale = s, frame = fi, frames = #spr.frames })
'@
            $path = Resolve-UserPath (Get-Arg $a 'path')
            if (-not (Test-Path -LiteralPath $path)) { throw "file not found: $path" }
            $out = Join-Path $script:TempDir ([guid]::NewGuid().ToString('N') + '.png')
            $scale = Get-Arg $a 'scale'
            $params = @{
                path = $path; out = $out
                frame = [int](Get-Arg $a 'frame' 1)
                scale = $(if ($null -ne $scale) { [int]$scale } else { $null })
                max_size = [int](Get-Arg $a 'max_size' 512)
                checker = [bool](Get-Arg $a 'checker' $true)
            }
            try {
                $info = Invoke-LuaTool $body $params
                $b64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($out))
            } finally {
                Remove-Item $out -ErrorAction SilentlyContinue
            }
            return @{ content = @(@{ type = 'image'; data = $b64; mimeType = 'image/png' }, @{ type = 'text'; text = $info }); isError = $false }
        }
        'aseprite_export' {
            $path = Resolve-UserPath (Get-Arg $a 'path')
            if (-not (Test-Path -LiteralPath $path)) { throw "file not found: $path" }
            $output = Resolve-UserPath (Get-Arg $a 'output')
            Assert-ParentDir $output
            $argList = '--batch'
            $layer = Get-Arg $a 'layer'; if ($layer) { $argList += ' --layer ' + (Format-Arg $layer) }
            $tag = Get-Arg $a 'tag'; if ($tag) { $argList += ' --tag ' + (Format-Arg $tag) }
            $from = Get-Arg $a 'frame_from'; $to = Get-Arg $a 'frame_to'
            if ($null -ne $from -or $null -ne $to) {
                if ($null -eq $from) { $from = $to }; if ($null -eq $to) { $to = $from }
                $argList += " --frame-range $([int]$from),$([int]$to)"
            }
            $argList += ' ' + (Format-Arg $path)
            $scale = Get-Arg $a 'scale'
            if ($null -ne $scale) { $argList += ' --scale ' + [string]::Format([Globalization.CultureInfo]::InvariantCulture, '{0}', $scale) }
            $argList += ' --save-as ' + (Format-Arg $output)
            $start = (Get-Date).AddSeconds(-1)
            $r = Invoke-Aseprite $argList
            $files = Get-NewFiles $output $start
            if ($r.exit -ne 0 -or $files.Count -eq 0) { throw "export failed (exit $($r.exit)).`nstdout: $($r.out)`nstderr: $($r.err)" }
            return New-TextResult ("Exported $($files.Count) file(s):`n" + ($files -join "`n"))
        }
        'aseprite_export_sheet' {
            $path = Resolve-UserPath (Get-Arg $a 'path')
            if (-not (Test-Path -LiteralPath $path)) { throw "file not found: $path" }
            $sheet = Resolve-UserPath (Get-Arg $a 'sheet')
            Assert-ParentDir $sheet
            $argList = '--batch'
            $layer = Get-Arg $a 'layer'; if ($layer) { $argList += ' --layer ' + (Format-Arg $layer) }
            $tag = Get-Arg $a 'tag'; if ($tag) { $argList += ' --tag ' + (Format-Arg $tag) }
            if (Get-Arg $a 'split_layers' $false) { $argList += ' --split-layers' }
            if (Get-Arg $a 'split_tags' $false) { $argList += ' --split-tags' }
            if (Get-Arg $a 'trim' $false) { $argList += ' --trim' }
            $argList += ' ' + (Format-Arg $path)
            $scale = Get-Arg $a 'scale'
            if ($null -ne $scale) { $argList += ' --scale ' + [string]::Format([Globalization.CultureInfo]::InvariantCulture, '{0}', $scale) }
            $argList += ' --sheet ' + (Format-Arg $sheet)
            $argList += ' --sheet-type ' + [string](Get-Arg $a 'sheet_type' 'horizontal')
            $cols = Get-Arg $a 'columns'; if ($null -ne $cols) { $argList += " --sheet-columns $([int]$cols)" }
            $pad = Get-Arg $a 'padding'; if ($null -ne $pad) { $argList += " --shape-padding $([int]$pad)" }
            $data = Get-Arg $a 'data'
            if ($data) {
                $data = Resolve-UserPath $data
                Assert-ParentDir $data
                $argList += ' --data ' + (Format-Arg $data) + ' --format json-array --list-tags --list-layers --list-slices'
            }
            $r = Invoke-Aseprite $argList
            if ($r.exit -ne 0 -or -not (Test-Path -LiteralPath $sheet)) { throw "sheet export failed (exit $($r.exit)).`nstdout: $($r.out)`nstderr: $($r.err)" }
            $msg = "Sheet: $sheet"
            if ($data) { $msg += "`nData: $data" }
            return New-TextResult $msg
        }
        'aseprite_run_lua' {
            $code = [string](Get-Arg $a 'code')
            if ([string]::IsNullOrWhiteSpace($code)) { throw 'code is required' }
            $sprite = Get-Arg $a 'sprite_path'
            if ($sprite) {
                $sprite = Resolve-UserPath $sprite
                if (-not (Test-Path -LiteralPath $sprite)) { throw "file not found: $sprite" }
            }
            $file = Join-Path $script:TempDir ([guid]::NewGuid().ToString('N') + '.lua')
            [IO.File]::WriteAllText($file, $LuaPrelude + "`n" + $code, $Utf8)
            try {
                $argList = '--batch '
                if ($sprite) { $argList += (Format-Arg $sprite) + ' ' }
                $argList += '--script ' + (Format-Arg $file)
                $r = Invoke-Aseprite $argList ([int](Get-Arg $a 'timeout_sec' 120))
            } finally {
                Remove-Item $file -ErrorAction SilentlyContinue
            }
            $text = "exit: $($r.exit)`n--- stdout ---`n$($r.out)"
            if ($r.err) { $text += "`n--- stderr ---`n$($r.err)" }
            return New-TextResult $text ($r.exit -ne 0)
        }
        default { throw "unknown tool: $name" }
    }
}

# ---------------------------------------------------------------- main loop

while ($true) {
    $line = $reader.ReadLine()
    if ($null -eq $line) { break }
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    try {
        $msg = ConvertFrom-Json $line
    } catch {
        Send-Message @{ jsonrpc = '2.0'; id = $null; error = @{ code = -32700; message = 'Parse error' } }
        continue
    }
    $hasId = $null -ne $msg.PSObject.Properties['id']
    $method = [string]$msg.method
    $result = $null
    try {
        # Notifications (no id) need no reply.
        if (-not $hasId) { continue }
        switch ($method) {
            'initialize' {
                $version = Get-Arg $msg.params 'protocolVersion' '2025-06-18'
                $result = @{
                    protocolVersion = $version
                    capabilities = @{ tools = @{} }
                    serverInfo = @{ name = 'aseprite-mcp'; version = '1.0.0' }
                    instructions = "Aseprite: $(if ($script:AsepriteExe) { $script:AsepriteExe } else { 'NOT FOUND - set ASEPRITE_PATH' }). Relative paths resolve against $($script:BaseDir)."
                }
            }
            'ping' { $result = @{} }
            'tools/list' { $result = @{ tools = $Tools } }
            'tools/call' {
                try {
                    $result = Invoke-Tool ([string]$msg.params.name) $msg.params.arguments
                } catch {
                    $result = New-TextResult ("Error: " + $_.Exception.Message) $true
                }
            }
            default { throw [System.MissingMethodException]::new("Method not found: $method") }
        }
        Send-Message @{ jsonrpc = '2.0'; id = $msg.id; result = $result }
    } catch [System.MissingMethodException] {
        Send-Message @{ jsonrpc = '2.0'; id = $msg.id; error = @{ code = -32601; message = $_.Exception.Message } }
    } catch {
        Send-Message @{ jsonrpc = '2.0'; id = $msg.id; error = @{ code = -32603; message = $_.Exception.Message } }
    }
}
