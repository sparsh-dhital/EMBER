# Installs the approved MakeHuman asset packs into MPFB's user data dir, the same way
# MPFB's own "Load pack from zip file" operator does (check, fix if needed, extract).
import zipfile, os
from bl_ext.user_default.mpfb.services import AssetService, LocationService

D = "C:/LudusForge/Tools/downloads/"
packs = ["makehuman_system_assets_cc0.zip", "shirts02_ccby.zip", "hats03_cc-by.zip",
         "shoes03_ccby.zip", "gloves01_cc0.zip", "bodyparts05_cc0.zip"]
data_dir = LocationService.get_user_data()
print("DATA DIR", data_dir)
for p in packs:
    f = D + p
    check = AssetService.check_asset_pack_zip(f)
    if check is not None:
        res = AssetService.fix_and_extract_asset_pack_zip(f, data_dir)
        print("PACK", p, "FIXED" if res is None else "FIX FAILED: " + str(res))
    else:
        with zipfile.ZipFile(f, "r") as z:
            z.extractall(data_dir)
        print("PACK", p, "OK")
AssetService.update_all_asset_lists()
print("SYSTEM ASSETS INSTALLED:", AssetService.system_assets_pack_is_installed())
for kind in ["clothes", "skins", "eyebrows", "eyelashes", "hair", "eyes", "teeth", "proxymeshes"]:
    d = os.path.join(data_dir, kind)
    print(kind, len(os.listdir(d)) if os.path.isdir(d) else 0)
