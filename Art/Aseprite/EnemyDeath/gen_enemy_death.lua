-- Enemy death animation generator (4 frames, 32x32 each)
local ROOT = "D:/20260611Cube/"
local SRC  = ROOT .. "Assets/Sprites/Enemy/"
local OUT_PNG = ROOT .. "Assets/Sprites/Enemy/Death/"
local OUT_ASE = ROOT .. "Art/Aseprite/EnemyDeath/"
local C = 32
local pc = app.pixelColor

local enemies = {
  {"Enemy_Goblin","poof"},{"Boss_GoblinChief","poof"},{"Speed_GoblinScout","poof"},
  {"Enemy_Skeleton","crumble"},{"Boss_SkeletonKnight","crumble"},{"Speed_SkullBat","crumble"},
  {"Enemy_Orc","topple"},{"Boss_OrcWarlord","topple"},{"Speed_Boar","topple"},
  {"Enemy_DarkElf","dissolve"},{"Boss_DarkElfArchmage","dissolve"},{"Speed_ShadowSpider","dissolve"},
  {"Enemy_Devil","burn"},{"Boss_DemonLord","burn"},{"Speed_Imp","burn"},
}

-- deterministic rng
local seed = 1
local function srand(s) seed = s end
local function rnd() seed = (seed * 1103515245 + 12345) % 2147483648; return seed / 2147483648 end
local function hash(x, y, k) -- stable per-pixel noise
  local h = (x * 374761393 + y * 668265263 + k * 2147483647) % 2147483648
  h = (h ~ (h >> 13)) * 1274126177 % 2147483648
  return (h % 10007) / 10007
end

local function clamp(v) if v < 0 then return 0 elseif v > 255 then return 255 end return math.floor(v + 0.5) end
local function rgba(r,g,b,a) return pc.rgba(clamp(r),clamp(g),clamp(b),clamp(a or 255)) end
local function lerpc(c, r,g,b, t)
  return rgba(pc.rgbaR(c)+(r-pc.rgbaR(c))*t, pc.rgbaG(c)+(g-pc.rgbaG(c))*t, pc.rgbaB(c)+(b-pc.rgbaB(c))*t, pc.rgbaA(c))
end
local function mul(c, k) return rgba(pc.rgbaR(c)*k, pc.rgbaG(c)*k, pc.rgbaB(c)*k, pc.rgbaA(c)) end

-- read frame 0 of source into a table px[y][x] (nil = transparent)
local function readBody(path)
  local spr = app.open(path)
  local img = Image(spr.width, spr.height, ColorMode.RGB)
  img:drawSprite(spr, 1)
  local px, minX, maxX, minY, maxY = {}, 99, -1, 99, -1
  for y = 0, C-1 do
    px[y] = {}
    for x = 0, C-1 do
      local c = img:getPixel(x, y)
      if pc.rgbaA(c) > 0 then
        px[y][x] = c
        if x < minX then minX = x end; if x > maxX then maxX = x end
        if y < minY then minY = y end; if y > maxY then maxY = y end
      end
    end
  end
  spr:close()
  return { px = px, minX = minX, maxX = maxX, minY = minY, maxY = maxY }
end

local function each(b, fn) for y = 0, C-1 do for x = 0, C-1 do local c = b.px[y][x]; if c then fn(x, y, c) end end end end
local function put(img, x, y, c) x = math.floor(x + 0.5); y = math.floor(y + 0.5); if x >= 0 and x < C and y >= 0 and y < C then img:drawPixel(x, y, c) end end

-- soft round puff
local function puff(img, cx, cy, r, c, edge)
  for y = math.floor(cy - r), math.ceil(cy + r) do
    for x = math.floor(cx - r), math.ceil(cx + r) do
      local d = math.sqrt((x - cx)^2 + (y - cy)^2)
      if d <= r then put(img, x, y, (d > r - 1 and edge) and edge or c) end
    end
  end
end

local function flash(img, b, r, g, bl, t)
  each(b, function(x, y, c) put(img, x, y, lerpc(c, r, g, bl, t)) end)
end

----------------------------------------------------------------- themes
local T = {}

-- World 1 : flash → squash → smoke puff → fading puffs
T.poof = function(b, f, img)
  local cx, floor = (b.minX + b.maxX) / 2, b.maxY
  local h = b.maxY - b.minY + 1
  local cy = floor - h * 0.4
  if f == 1 then flash(img, b, 255, 255, 255, 0.6)
  elseif f == 2 then
    -- squash: inverse map, sx 1.25, sy 0.55, anchored to floor
    for y = 0, C-1 do for x = 0, C-1 do
      local sx = cx + (x - cx) / 1.25
      local sy = floor - (floor - y) / 0.55
      local c = b.px[math.floor(sy + 0.5)] and b.px[math.floor(sy + 0.5)][math.floor(sx + 0.5)]
      if c then put(img, x, y, lerpc(c, 255, 255, 255, 0.25)) end
    end end
  elseif f == 3 then
    local gray, light, dark = rgba(208,208,200), rgba(244,244,236), rgba(120,120,120)
    srand(7)
    for i = 1, 7 do
      local a = i / 7 * math.pi * 2
      puff(img, cx + math.cos(a) * h * 0.28, cy + math.sin(a) * h * 0.2, h * 0.16 + rnd() * 1.5, gray, dark)
    end
    puff(img, cx, cy, h * 0.24, light, gray)
    -- scraps
    each(b, function(x, y, c) if hash(x, y, 3) < 0.08 then put(img, x, y - 3, c) end end)
  else
    local gray, dark = rgba(196,196,188,200), rgba(130,130,130,170)
    srand(11)
    for i = 1, 6 do
      local a = i / 6 * math.pi * 2 + 0.4
      puff(img, cx + math.cos(a) * h * 0.42, cy - 2 + math.sin(a) * h * 0.3, 1.6 + rnd() * 1.2, gray, dark)
    end
  end
end

-- World 2 : flash → bones drop → collapse into pile → dim scattered pile
T.crumble = function(b, f, img)
  local floor = b.maxY
  if f == 1 then flash(img, b, 255, 255, 255, 0.6); return end
  -- body breaks into 4-row chunks; each chunk falls (top ones fall farther) and squashes
  -- into a low pile (height * 0.3) with a little sideways scatter per chunk
  local h = b.maxY - b.minY + 1
  local k = ({0, 0.4, 0.85, 1})[f]
  local pileH = h * 0.3
  for y = C-1, 0, -1 do          -- bottom first so falling upper chunks land on top
    for x = 0, C-1 do
      local c = b.px[y][x]
      if c then
        local chunk = math.floor((y - b.minY) / 4)
        local rel = (b.maxY - y) / h                       -- 0 at feet, 1 at head
        local ty = b.maxY - rel * pileH
        local side = (hash(chunk, 0, 9) - 0.5) * 6 * rel
        local ny = y + (ty - y) * k
        local nx = x + side * k
        if f == 2 then nx = nx + (hash(chunk, 1, 5) - 0.5) * 2 end
        local col = (f == 4) and mul(c, 0.75) or c
        put(img, nx, ny, col)
      end
    end
  end
end

-- World 3 : flash → tip back → lie flat + dust → fade out dithered
T.topple = function(b, f, img)
  local floor = b.maxY
  local px0 = (b.minX + b.maxX) / 2
  if f == 1 then flash(img, b, 255, 255, 255, 0.6); return end
  local ang = ({0, 0.55, 1.5708, 1.5708})[f]   -- radians, falls backward (to the right)
  local ca, sa = math.cos(ang), math.sin(ang)
  -- rotate each source pixel around bottom-center pivot; collect
  local pts, minx, maxx = {}, 99, -99
  for dy = -C, C do for dx = -C, C do
    -- inverse rotate destination offset into source
    local sx = px0 + dx * ca + dy * sa
    local sy = floor - (-dx * sa + dy * ca)
    local c = b.px[math.floor(sy + 0.5)] and b.px[math.floor(sy + 0.5)][math.floor(sx + 0.5)]
    if c then
      local X, Y = px0 + dx, floor - dy
      pts[#pts + 1] = {X, Y, c}
      if X < minx then minx = X end; if X > maxx then maxx = X end
    end
  end end
  local shift = 0
  if maxx > C - 1 then shift = (C - 1) - maxx end
  if minx + shift < 0 then shift = -minx end
  for _, p in ipairs(pts) do
    -- lying on the ground reads better squashed (top-down-ish foreshortening)
    local sq = (f >= 3) and 0.5 or 1
    local X = math.floor(p[1] + shift + 0.5)
    local Y = math.floor(floor - (floor - p[2]) * sq + 0.5)
    local col = p[3]
    if f == 4 then
      if (X + Y) % 2 == 0 then col = nil else col = mul(col, 0.65) end
    end
    if col then put(img, X, Y, col) end
  end
  if f >= 3 then
    local dust, dark = rgba(186,160,120, f == 3 and 255 or 170), rgba(130,108,80, f == 3 and 255 or 150)
    local spread = (f == 3) and 1 or 1.8
    local base = (minx + maxx) / 2 + shift
    srand(23)
    for i = 0, 4 do
      local x = base + (i - 2) * 5 * spread + (rnd() - 0.5) * 2
      puff(img, x, floor - (f == 3 and 1 or 2), (f == 3) and 1.6 or 1.1, dust, dark)
    end
  end
end

-- World 4 : cyan flash → pixels turn to arcane motes rising → mostly motes → last motes
T.dissolve = function(b, f, img)
  if f == 1 then flash(img, b, 170, 240, 255, 0.7); return end
  local p = ({0, 0.4, 0.75, 1.1})[f]
  local motes = { rgba(120,230,255), rgba(190,140,255), rgba(255,255,255) }
  each(b, function(x, y, c)
    local t = hash(x, y, 17)
    if t > p then
      put(img, x, y, lerpc(c, 150, 110, 220, 0.25 * f))
    elseif t > p - 0.35 then
      local rise = (p - t) * 22
      if hash(x, y, 29) < 0.45 then
        local drift = math.sin(y * 0.7 + x) * 1.2
        put(img, x + drift, y - rise, motes[1 + math.floor(hash(x, y, 31) * 3)])
      end
    end
  end)
end

-- World 5 : orange flash → burn from bottom with glowing edge + embers → nearly ash → embers + ash
T.burn = function(b, f, img)
  if f == 1 then flash(img, b, 255, 150, 60, 0.7); return end
  local h = b.maxY - b.minY + 1
  local level = ({0, 0.35, 0.72, 1.15})[f]   -- fraction of height consumed (from bottom)
  local edgeY = b.maxY - level * h
  local yellow, orange, red = rgba(255,236,120), rgba(255,140,40), rgba(200,50,30)
  each(b, function(x, y, c)
    local n = (hash(x, y, 41) - 0.5) * 4
    local d = y - edgeY + n
    if d < -2 then
      put(img, x, y, lerpc(c, 60, 20, 20, 0.35 + 0.1 * f))      -- charring body
    elseif d < 0 then
      put(img, x, y, yellow)                                   -- burning edge
    elseif d < 1.5 then
      put(img, x, y, orange)
    elseif hash(x, y, 43) < 0.18 then
      -- ember rising from consumed area
      local rise = 2 + hash(x, y, 47) * 6 + (f - 2) * 3
      put(img, x + math.sin(y + x) * 1.5, y - rise, (hash(x, y, 53) < 0.5) and orange or red)
    end
  end)
  -- ash pile
  local ash, ashd = rgba(90,80,80), rgba(60,52,52)
  if f >= 3 then
    local w = math.floor((b.maxX - b.minX) * (f == 3 and 0.4 or 0.55))
    local cx = (b.minX + b.maxX) / 2
    for x = math.floor(cx - w / 2), math.floor(cx + w / 2) do
      put(img, x, b.maxY, ashd)
      if math.abs(x - cx) < w / 3 then put(img, x, b.maxY - 1, ash) end
    end
  end
end

----------------------------------------------------------------- build
local DUR = { 0.08, 0.10, 0.11, 0.12 }   -- ~0.41s, matches EnemyDeathEffect length
for _, e in ipairs(enemies) do
  local name, theme = e[1], e[2]
  local body = readBody(SRC .. name .. ".png")

  local spr = Sprite(C, C, ColorMode.RGB)
  spr.layers[1].name = "death"
  for i = 2, 4 do spr:newEmptyFrame() end
  for f = 1, 4 do
    local img = Image(C, C, ColorMode.RGB)
    T[theme](body, f, img)
    spr:newCel(spr.layers[1], spr.frames[f], img, Point(0, 0))
    spr.frames[f].duration = DUR[f]
  end
  local tag = spr:newTag(1, 4); tag.name = "death"
  spr:saveAs(OUT_ASE .. name .. "_Death.aseprite")

  -- horizontal 128x32 sheet, same layout as walk sheets
  local sheet = Image(C * 4, C, ColorMode.RGB)
  for f = 1, 4 do sheet:drawImage(spr.layers[1]:cel(f).image, Point((f - 1) * C + spr.layers[1]:cel(f).position.x, spr.layers[1]:cel(f).position.y)) end
  sheet:saveAs(OUT_PNG .. name .. "_Death.png")
  spr:close()
  print("ok", name, theme)
end
