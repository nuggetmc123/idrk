"""What each material's texture files are called: textures/<NAME>_Color.jpg and <NAME>_Normal.jpg.
Shared by make_textures.py and make_warehouse.py."""
TEXTURE_NAMES = {
    "MetalWall":    "Corrugated_Metal_Wall",
    "Roof":         "Galvanized_Metal_Roof",
    "Concrete":     "Concrete_Floor",
    "GarageDoor":   "Garage_Door_Panels",
    "Steel":        "Galvanized_Steel",
    "Trim":         "Yellow_Safety_Paint",
    "ShelfFrame":   "Orange_Shelf_Paint",
    "Wood":         "Plywood",
    "OfficeWall":   "Painted_Drywall",
    "RoomWall":     "White_Ceiling",
    "BathroomWall": "White_Wall_Tiles",
    "Tile":         "Grey_Floor_Tiles",
    "Carpet":       "Office_Carpet",
    "Cabinet":      "Beige_Metal_Paint",
}

def color_file(material): return f"textures/{TEXTURE_NAMES[material]}_Color.jpg"
def normal_file(material): return f"textures/{TEXTURE_NAMES[material]}_Normal.jpg"
