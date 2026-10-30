-- Special bosses (Evolve / Dungeon): 48x48, walk 4 frames + death 4 frames
-- Run: aseprite -b --script gen_special_bosses.lua
local ROOT    = "D:/20260611Cube/"
local OUT_ASE = ROOT .. "Art/Aseprite/Enemy/"
local OUT_WALK  = ROOT .. "Assets/Sprites/Enemy/"
local OUT_DEATH = ROOT .. "Assets/Sprites/Enemy/Death/"
local S  = 48
local pc = app.pixelColor

----------------------------------------------------------------- helpers
local function clamp(v) if v < 0 then return 0 elseif v > 255 then return 255 end return math.floor(v + 0.5) end
local function hex(h, a)
  return pc.rgba(tonumber(h:sub(2,3),16), tonumber(h:sub(4,5),16), tonumber(h:sub(6,7),16), a or 255)
end
local function rgba(r,g,b,a) return pc.rgba(clamp(r),clamp(g),clamp(b),clamp(a or 255)) end
local function lerpc(c, r,g,b, t)
  return rgba(pc.rgbaR(c)+(r-pc.rgbaR(c))*t, pc.rgbaG(c)+(g-pc.rgbaG(c))*t, pc.rgbaB(c)+(b-pc.rgbaB(c))*t, pc.rgbaA(c))
end
local function mul(c, k) return rgba(pc.rgbaR(c)*k, pc.rgbaG(c)*k, pc.rgbaB(c)*k, pc.rgbaA(c)) end
local function hash(x, y, k)
  local h = (x * 374761393 + y * 668265263 + k * 2147483647) % 2147483648
  h = (h ~ (h >> 13)) * 1274126177 % 2147483648
  return (h % 10007) / 10007
end

local function newImg() return Image(S, S, ColorMode.RGB) end
local function put(img, x, y, c)
  x = math.floor(x + 0.5); y = math.floor(y + 0.5)
  if x >= 0 and x < S and y >= 0 and y < S then img:drawPixel(x, y, c) end
end
local function get(img, x, y)
  if x < 0 or y < 0 or x >= S or y >= S then return nil end
  local c = img:getPixel(x, y)
  if pc.rgbaA(c) == 0 then return nil end
  return c
end

local O = hex("#1e1a24")   -- outline, same feel as the other enemy sheets

-- mask helpers
local function rrect(x0, y0, x1, y1, r)
  return function(x, y)
    if x < x0 or x > x1 or y < y0 or y > y1 then return false end
    local cx = (x < x0 + r) and (x0 + r) or ((x > x1 - r) and (x1 - r) or x)
    local cy = (y < y0 + r) and (y0 + r) or ((y > y1 - r) and (y1 - r) or y)
    return (x - cx)^2 + (y - cy)^2 <= (r + 0.35)^2
  end
end
local function ellipse(cx, cy, rx, ry)
  return function(x, y) return ((x - cx) / rx)^2 + ((y - cy) / ry)^2 <= 1.05 end
end
local function diamond(cx, cy, r)
  return function(x, y) return math.abs(x - cx) + math.abs(y - cy) <= r end
end

-- three-tone shading by position inside a box (light top-left rim, dark bottom-right)
local function shader(x0, y0, x1, y1, light, mid, dark)
  return function(x, y)
    if x >= x1 - 1 or y >= y1 - 1 then return dark end
    if y <= y0 + 1 or x <= x0 then return light end
    return mid
  end
end

-- draw a part: fill mask, 1px outline around it (outline paints over earlier parts → separates overlaps)
local function part(img, mask, color, bx0, by0, bx1, by1)
  for y = by0 - 1, by1 + 1 do
    for x = bx0 - 1, bx1 + 1 do
      if mask(x, y) then
        put(img, x, y, type(color) == "function" and color(x, y) or color)
      elseif mask(x-1,y) or mask(x+1,y) or mask(x,y-1) or mask(x,y+1) then
        put(img, x, y, O)
      end
    end
  end
end

local function fill(img, mask, color, bx0, by0, bx1, by1)
  for y = by0, by1 do for x = bx0, bx1 do
    if mask(x, y) then put(img, x, y, type(color) == "function" and color(x, y) or color) end
  end end
end

local function line(img, x0, y0, x1, y1, c)
  local n = math.max(math.abs(x1 - x0), math.abs(y1 - y0))
  for i = 0, n do
    local t = (n == 0) and 0 or i / n
    put(img, x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, c)
  end
end

local function blit(dst, src, dx, dy, tint)
  for y = 0, S-1 do for x = 0, S-1 do
    local c = get(src, x, y)
    if c then put(dst, x + dx, y + dy, tint and tint(c, x, y) or c) end
  end end
end

local function puff(img, cx, cy, r, c, edge)
  for y = math.floor(cy - r), math.ceil(cy + r) do
    for x = math.floor(cx - r), math.ceil(cx + r) do
      local d = math.sqrt((x - cx)^2 + (y - cy)^2)
      if d <= r then put(img, x, y, (d > r - 1 and edge) and edge or c) end
    end
  end
end

local function sparkle(img, x, y, c, core)
  put(img, x, y, core or c)
  put(img, x-1, y, c); put(img, x+1, y, c); put(img, x, y-1, c); put(img, x, y+1, c)
end

local FLOOR = 45   -- lowest body row (outline at 46)

----------------------------------------------------------------- EVOLVE BOSS : moss stone golem with a crystal core
local G = {
  sL = hex("#a3acbf"), sM = hex("#727b93"), sD = hex("#4b5168"),
  mL = hex("#86c34a"), mD = hex("#4d8a2c"),
  cW = hex("#f0ffff"), cL = hex("#8ff4ff"), cM = hex("#3fd0ea"), cD = hex("#1f8fb3"),
  crack = hex("#383d50"),
}

-- p: {bob, lLeg, rLeg, lArm, rArm, glow}
local function drawGolem(p)
  local img = newImg()
  local b = p.bob or 0
  -- legs (planted unless lifted)
  local function leg(x0, lift)
    local y0, y1 = 37 - lift, FLOOR - lift
    part(img, rrect(x0, y0, x0 + 6, y1, 1), shader(x0, y0, x0 + 6, y1, G.sL, G.sM, G.sD), x0, y0, x0 + 6, y1)
    for x = x0, x0 + 6 do put(img, x, y1 - 2, G.sD) end   -- toe line
  end
  leg(15, p.lLeg or 0); leg(27, p.rLeg or 0)

  -- torso
  local tx0, ty0, tx1, ty1 = 11, 18 + b, 37, 39 + b
  part(img, rrect(tx0, ty0, tx1, ty1, 5), shader(tx0, ty0, tx1, ty1, G.sL, G.sM, G.sD), tx0, ty0, tx1, ty1)
  -- cracks
  line(img, 14, 29 + b, 17, 33 + b, G.crack); line(img, 17, 33 + b, 16, 36 + b, G.crack)
  line(img, 33, 22 + b, 35, 26 + b, G.crack)
  -- moss on chest top
  for x = 13, 20 do put(img, x, ty0 + 1, G.mL) end
  for x = 14, 17 do put(img, x, ty0 + 2, G.mD) end

  -- crystal core
  local ccx, ccy = 24, 28 + b
  local glow = p.glow or 0
  fill(img, diamond(ccx, ccy, 5), G.cD, ccx - 5, ccy - 5, ccx + 5, ccy + 5)
  fill(img, diamond(ccx, ccy, 4), function(x, y) return (x > ccx) and G.cM or G.cL end, ccx - 4, ccy - 4, ccx + 4, ccy + 4)
  fill(img, diamond(ccx - 1, ccy - 1, 1 + glow), G.cW, ccx - 3, ccy - 3, ccx + 1, ccy + 1)

  -- head
  local hx0, hy0, hx1, hy1 = 17, 6 + b, 31, 19 + b
  part(img, rrect(hx0, hy0, hx1, hy1, 3), shader(hx0, hy0, hx1, hy1, G.sL, G.sM, G.sD), hx0, hy0, hx1, hy1)
  for x = hx0 + 2, hx1 - 3 do put(img, x, hy0, G.mL) end
  for x = hx0 + 3, hx0 + 7 do put(img, x, hy0 + 1, G.mD) end
  for x = hx0 + 1, hx1 - 1 do put(img, x, 12 + b, G.sD) end          -- brow
  for _, ex in ipairs({20, 26}) do                                      -- glowing eyes
    put(img, ex, 13 + b, G.cL); put(img, ex + 1, 13 + b, G.cW)
    put(img, ex, 14 + b, G.cM); put(img, ex + 1, 14 + b, G.cL)
  end
  for x = 22, 26 do put(img, x, 17 + b, G.sD) end                       -- mouth

  -- arms (in front of torso edge) with big fists
  local function arm(x0, dy)
    local y0 = 19 + b + dy
    part(img, rrect(x0, y0, x0 + 7, y0 + 13, 3), shader(x0, y0, x0 + 7, y0 + 13, G.sL, G.sM, G.sD), x0, y0, x0 + 7, y0 + 13)
    local fy = y0 + 11
    part(img, rrect(x0 - 1, fy, x0 + 8, fy + 7, 3), shader(x0 - 1, fy, x0 + 8, fy + 7, G.sL, G.sM, G.sD), x0 - 1, fy, x0 + 8, fy + 7)
    for x = x0 + 1, x0 + 5 do put(img, x, y0 + 1, G.mL) end             -- shoulder moss
    put(img, x0 + 2, fy + 3, G.sD); put(img, x0 + 5, fy + 3, G.sD)      -- knuckles
  end
  arm(3, p.lArm or 0); arm(37, p.rArm or 0)
  return img
end

local golemWalk = {
  {bob = 0, lLeg = 2, rLeg = 0, lArm = -1, rArm = 1,  glow = 0},
  {bob = 1, lLeg = 0, rLeg = 0, lArm = 0,  rArm = 0,  glow = 1},
  {bob = 0, lLeg = 0, rLeg = 2, lArm = 1,  rArm = -1, glow = 0},
  {bob = 1, lLeg = 0, rLeg = 0, lArm = 0,  rArm = 0,  glow = 1},
}

local function golemDeath(f)
  local base = drawGolem({bob = 0, glow = 1})
  local img = newImg()
  if f == 1 then
    blit(img, base, 0, 0, function(c) return lerpc(c, 220, 255, 255, 0.6) end)
    return img
  end
  if f == 2 then
    -- cracks of light split the body, crystal flares
    blit(img, base, 0, 0, function(c) return mul(c, 0.85) end)
    local cracks = {
      {24,28, 14,20}, {24,28, 34,19}, {24,28, 12,38}, {24,28, 36,37}, {24,28, 24,9}, {14,20, 8,26}, {34,19, 40,25},
    }
    for _, l in ipairs(cracks) do
      local x0, y0, x1, y1 = l[1], l[2], l[3], l[4]
      local n = math.max(math.abs(x1 - x0), math.abs(y1 - y0))
      for i = 0, n do
        local t = i / n
        local x = math.floor(x0 + (x1 - x0) * t + (hash(i, x1, 3) - 0.5) * 1.6 + 0.5)
        local y = math.floor(y0 + (y1 - y0) * t + 0.5)
        if get(base, x, y) then put(img, x, y, (i % 3 == 0) and G.cW or G.cL) end
      end
    end
    fill(img, diamond(24, 28, 5), G.cW, 19, 23, 29, 33)
    fill(img, diamond(24, 28, 3), G.cL, 21, 25, 27, 31)
    return img
  end
  -- f3 / f4 : the body breaks into 6x6 chunks that fall into a rubble pile; shards fly; dust rises
  local k = (f == 3) and 0.55 or 1
  local CH = 6
  local minY, maxY = 6, FLOOR
  local h = maxY - minY + 1
  for cy = math.floor(maxY / CH), 0, -1 do           -- bottom chunks first, upper ones land on top
    for cx = 0, math.floor((S - 1) / CH) do
      local rel = ((cy + 0.5) * CH - minY) / h
      rel = 1 - math.max(0, math.min(1, rel))         -- 0 at feet, 1 at head
      local chunkBottom = (cy + 1) * CH - 1
      local targetBottom = FLOOR - rel * h * 0.32
      local dy = (math.min(targetBottom, FLOOR) - chunkBottom) * k
      if dy < 0 then dy = dy * 0.2 end
      local dx = (hash(cx, cy, 9) - 0.5) * 12 * rel * k
      for y = cy * CH, cy * CH + CH - 1 do for x = cx * CH, cx * CH + CH - 1 do
        local c = get(base, x, y)
        if c then
          if f == 4 then c = mul(c, 0.8) end
          local ny = math.min(y + dy, FLOOR + 1)
          put(img, x + dx, ny, c)
        end
      end end
    end
  end
  -- crystal shards
  local n = (f == 3) and 9 or 6
  for i = 1, n do
    local a = (i / n) * math.pi * 2 + 0.3
    local r = (f == 3) and (8 + hash(i, 1, 2) * 6) or (13 + hash(i, 2, 2) * 6)
    local x = 24 + math.cos(a) * r
    local y = 26 + math.sin(a) * r * 0.8 + ((f == 4) and 4 or 0)
    if f == 3 or i % 2 == 0 then
      put(img, x, y, G.cL); put(img, x + 1, y, G.cM); put(img, x, y - 1, G.cW)
    end
  end
  -- dust
  local dust, dustE = hex("#c9c3b5", (f == 3) and 255 or 200), hex("#8f897c", (f == 3) and 255 or 170)
  for i = 0, 4 do
    local x = 7 + i * 8.5 + (hash(i, f, 4) - 0.5) * 3
    local y = FLOOR - ((f == 3) and 2 or 5) - hash(i, f, 6) * 2
    local r = (f == 3) and 3.2 or 2.4
    puff(img, x, y, r, dust, dustE)
    puff(img, x + r * 0.9, y + 1, r * 0.7, dust, dustE)
  end
  return img
end

----------------------------------------------------------------- DUNGEON BOSS : treasure-chest mimic
local M = {
  wL = hex("#c98f52"), wM = hex("#94602f"), wD = hex("#5f3b1c"),
  gL = hex("#ffe58a"), gM = hex("#f0b434"), gD = hex("#a86e14"),
  mouth = hex("#3a0f1c"), mouth2 = hex("#5a1a2a"),
  teeth = hex("#f6f1e2"), teethD = hex("#c9c0aa"),
  tL = hex("#f27a95"), tD = hex("#b8405e"),
  eye = hex("#ffe14a"), eyeG = hex("#fff7c2"),
  coinL = hex("#fff1a8"), coinM = hex("#f5c242"), coinD = hex("#b9811a"),
}

-- 4x4 round coin with outline: .XX. / XXXX / XXXX / .XX.
local COIN = {
  {0,1,1,0},
  {1,1,1,1},
  {1,1,1,1},
  {0,1,1,0},
}
local function coin(img, x, y)
  x = math.floor(x + 0.5) - 2; y = math.floor(y + 0.5) - 2
  local function on(i, j) return COIN[j] and COIN[j][i] == 1 end
  for j = 0, 5 do for i = 0, 5 do
    if on(i, j) then
      local c = M.coinM
      if i + j <= 3 then c = M.coinL elseif i + j >= 6 then c = M.coinD end
      put(img, x + i - 1, y + j - 1, c)
    elseif on(i-1, j) or on(i+1, j) or on(i, j-1) or on(i, j+1) then
      put(img, x + i - 1, y + j - 1, O)
    end
  end end
end

-- p: {dy, open, squash, eyes = "open"|"x", tongue = dx, noLid, noBox, lidDx, lidDy}
local function drawMimic(p)
  local img = newImg()
  local dy, open = p.dy or 0, p.open or 1
  local sq = p.squash or 0
  -- box
  local bx0, by0, bx1, by1 = 9, 29 + dy + sq, 38, FLOOR + dy
  local mouthTop = 22 + dy - open * 2 + sq
  if not p.noBox then
    part(img, rrect(bx0, by0, bx1, by1, 1), shader(bx0, by0, bx1, by1, M.wL, M.wM, M.wD), bx0, by0, bx1, by1)
    for x = bx0, bx1 do put(img, x, math.floor((by0 + by1) / 2) + 1, M.wD) end   -- plank seam
    for _, gx in ipairs({12, 34}) do                                              -- gold bands
      for y = by0, by1 do put(img, gx, y, (y == by0) and M.gL or M.gM); put(img, gx + 1, y, M.gD) end
    end
    for x = bx0, bx1 do put(img, x, by0, M.gL); put(img, x, by0 + 1, M.gM) end    -- gold rim
    -- lock plate
    part(img, rrect(21, by0 + 3, 26, by0 + 8, 1), function(x, y) return (x >= 25 or y >= by0 + 8) and M.gD or M.gM end, 21, by0 + 3, 26, by0 + 8)
    put(img, 23, by0 + 5, O); put(img, 24, by0 + 5, O); put(img, 23, by0 + 6, O); put(img, 24, by0 + 6, O)
  end

  -- mouth interior (between lid and box)
  if not p.noBox and not p.noLid then
    local my0, my1 = mouthTop, by0 - 1
    if my1 >= my0 then
      for y = my0, my1 do for x = bx0 + 1, bx1 - 1 do put(img, x, y, (y < my0 + 2) and M.mouth2 or M.mouth) end end
      for x = bx0, bx1 do if get(img, x, my0 - 1) == nil then put(img, x, my0 - 1, O) end end
      put(img, bx0, my0, O); put(img, bx1, my0, O)
      for y = my0, my1 do put(img, bx0, y, O); put(img, bx1, y, O) end
      -- coins glinting inside
      if my1 - my0 >= 3 then
        put(img, 15, my1, M.coinM); put(img, 16, my1, M.coinL); put(img, 31, my1, M.coinM); put(img, 32, my1 - 1, M.coinL)
      end
      -- eyes
      local ey = math.floor((my0 + my1) / 2)
      for _, ex in ipairs({17, 29}) do
        if p.eyes == "x" then
          put(img, ex, ey - 1, M.eye); put(img, ex + 2, ey - 1, M.eye); put(img, ex + 1, ey, M.eye)
          put(img, ex, ey + 1, M.eye); put(img, ex + 2, ey + 1, M.eye)
        else
          put(img, ex, ey, M.eye); put(img, ex + 1, ey, M.eyeG); put(img, ex + 2, ey, M.eye)
          put(img, ex + 1, ey - 1, M.eye); put(img, ex + 1, ey + 1, M.eye)
          put(img, ex + 1, ey, O)                                  -- slit pupil
        end
      end
      -- teeth: upper row hangs from lid, lower row rises from box rim
      for x = bx0 + 2, bx1 - 3, 4 do
        put(img, x, my0, M.teeth); put(img, x + 1, my0, M.teeth); put(img, x, my0 + 1, M.teethD)
        put(img, x + 2, my1, M.teeth); put(img, x + 3, my1, M.teeth); put(img, x + 3, my1 - 1, M.teethD)
      end
    end
  end

  -- lid
  if not p.noLid then
    local lx = p.lidDx or 0
    local ly = p.lidDy or 0
    local lx0, ly0, lx1, ly1 = 9 + lx, mouthTop - 11 + ly, 38 + lx, mouthTop - 1 + ly
    part(img, rrect(lx0, ly0, lx1, ly1, 4), shader(lx0, ly0, lx1, ly1, M.wL, M.wM, M.wD), lx0, ly0, lx1, ly1)
    for _, gx in ipairs({12, 34}) do
      for y = ly0 + 1, ly1 do put(img, gx + lx, y, M.gM); put(img, gx + 1 + lx, y, M.gD) end
    end
    for x = lx0 + 1, lx1 - 1 do put(img, x, ly1, M.gM); put(img, x, ly1 - 1, M.gL) end   -- gold lip
    for x = lx0 + 5, lx1 - 7 do put(img, x, ly0 + 1, M.wL) end                          -- top shine
  end

  -- tongue lolling over the box front
  if p.tongue and not p.noBox then
    local tx = 22 + p.tongue
    local ty0 = by0 - 1
    local len = p.tongueLen or 9
    part(img, function(x, y)
      return x >= tx and x <= tx + 4 and y >= ty0 and y <= ty0 + len and not (y == ty0 + len and (x == tx or x == tx + 4))
    end, function(x, y) return (x == tx + 2 and y > ty0 + 1) and M.tD or M.tL end, tx, ty0, tx + 4, ty0 + len)
  end
  return img
end

local mimicWalk = {   -- hopping
  {dy = 0,  open = 1, tongue = 0,  tongueLen = 8},
  {dy = 1,  open = 0, squash = 1,  tongue = 0, tongueLen = 6},
  {dy = -3, open = 3, tongue = 1,  tongueLen = 10},
  {dy = -1, open = 2, tongue = -1, tongueLen = 9},
}

local function mimicDeath(f)
  local img = newImg()
  if f == 1 then
    blit(img, drawMimic({open = 1, tongue = 0, tongueLen = 8}), 0, 0, function(c) return lerpc(c, 255, 250, 220, 0.6) end)
    return img
  end
  if f == 2 then
    -- lid blows open, eyes go X, coins burst out
    blit(img, drawMimic({open = 4, eyes = "x", tongue = 0, tongueLen = 11}), 0, 0)
    for i = 0, 4 do   -- coins spilling out of the mouth
      coin(img, 13 + i * 5.5, 25 - math.abs(i - 2) * 1.5 + hash(i, 4, 1) * 2)
    end
    sparkle(img, 10, 10, M.gL, M.eyeG); sparkle(img, 39, 8, M.gL, M.eyeG)
    return img
  end
  if f == 3 then
    -- box sags, lid flies off, coins arc outward
    blit(img, drawMimic({noLid = true, squash = 4, tongue = 0, tongueLen = 5}), 0, 0, function(c) return mul(c, 0.92) end)
    local lid = drawMimic({noBox = true, open = 2})   -- lid y 7..17
    blit(img, lid, -7, -5)
    for i = 0, 6 do   -- coins arcing outward
      local a = math.pi * (1.05 + i / 6 * 0.9)
      local r = 15 + hash(i, 5, 1) * 3
      coin(img, 24 + math.cos(a) * r * 1.2, 34 + math.sin(a) * r * 0.75)
    end
    sparkle(img, 6, 22, M.gL, M.eyeG); sparkle(img, 42, 18, M.gL, M.eyeG); sparkle(img, 24, 4, M.gL, M.eyeG)
    return img
  end
  -- f4: wrecked chest flat on the ground, lid fallen beside it, coins scattered
  local box = drawMimic({noLid = true, squash = 0, tongue = 0, tongueLen = 3})
  for y = 0, S - 1 do for x = 0, S - 1 do       -- squash vertically toward the floor
    local sy = FLOOR - (FLOOR - y) / 0.55
    local c = get(box, x, math.floor(sy + 0.5))
    if c then put(img, x, y, mul(c, 0.75)) end
  end end
  -- lid slumped shut on top of the wreck, a little askew
  local lid = drawMimic({noBox = true, open = 0})   -- lid y 11..21
  blit(img, lid, 3, 14, function(c) return mul(c, 0.75) end)
  for _, x in ipairs({4, 10, 40, 45}) do            -- coins scattered on the floor
    coin(img, x, FLOOR - 1)
  end
  sparkle(img, 14, 28, M.gL, M.eyeG); sparkle(img, 36, 31, M.gL, M.eyeG)
  return img
end

----------------------------------------------------------------- build
local WALK_MS  = 0.12
local DEATH_MS = { 0.08, 0.10, 0.11, 0.12 }

local function build(name, walkFn, walkParams, deathFn)
  local spr = Sprite(S, S, ColorMode.RGB)
  spr.layers[1].name = name
  for i = 2, 8 do spr:newEmptyFrame() end
  local walkSheet  = Image(S * 4, S, ColorMode.RGB)
  local deathSheet = Image(S * 4, S, ColorMode.RGB)
  for i = 1, 4 do
    local w = walkFn(walkParams[i])
    spr:newCel(spr.layers[1], spr.frames[i], w, Point(0, 0))
    spr.frames[i].duration = WALK_MS
    walkSheet:drawImage(w, Point((i - 1) * S, 0))

    local d = deathFn(i)
    spr:newCel(spr.layers[1], spr.frames[4 + i], d, Point(0, 0))
    spr.frames[4 + i].duration = DEATH_MS[i]
    deathSheet:drawImage(d, Point((i - 1) * S, 0))
  end
  local t1 = spr:newTag(1, 4); t1.name = "walk"; t1.aniDir = AniDir.FORWARD
  local t2 = spr:newTag(5, 8); t2.name = "death"
  spr:saveAs(OUT_ASE .. name .. ".aseprite")
  walkSheet:saveAs(OUT_WALK .. name .. ".png")
  deathSheet:saveAs(OUT_DEATH .. name .. "_Death.png")
  spr:close()
  print("ok", name)
end

build("Boss_EvolveGolem",  drawGolem, golemWalk, golemDeath)
build("Boss_DungeonMimic", drawMimic, mimicWalk, mimicDeath)
