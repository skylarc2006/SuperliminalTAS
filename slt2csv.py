import csv
import sys
from pathlib import Path
import struct
import math

class InputFrame:
    def __init__(
        self,
        w: bool,
        a: bool,
        s: bool,
        d: bool,
        look_horizontal: float,
        look_vertical: float,
        jump_active: bool,
        grab_active: bool,
        rotate_active: bool
    ):
        self.w: bool = w
        self.a: bool = a
        self.s: bool = s
        self.d: bool = d
        self.look_horizontal: float = look_horizontal
        self.look_vertical: float = look_vertical
        self.jump_active: bool = jump_active
        self.grab_active: bool = grab_active
        self.rotate_active: bool = rotate_active
        
        
class NoPathPassed(Exception):
    pass

class NonexistentPath(Exception):
    pass

class PathIsNotFile(Exception):
    pass

class FileIsNotSltFile(Exception):
    pass

class InvalidSltMagic(Exception):
    pass

def approx_eq(a: float, b: float, eps: float = 1e-6) -> bool:
    return abs(a - b) < eps

def main():
    if len(sys.argv) <= 1:
        raise NoPathPassed("You must pass the path of a .slt file to this script!")
    
    slt_path = Path(sys.argv[1])
        
    if not slt_path.exists():
        raise NonexistentPath("Path does not exist!")
        
    elif not slt_path.is_file:
        raise PathIsNotFile("Path must be a file!")
        
    elif not slt_path.name.endswith(".slt"):
        raise FileIsNotSltFile("File must be .slt!")
    
    filename_prefix = slt_path.stem
    parent_directory = slt_path.parent
    
    SLT_MAGIC = b"SUPERLIMINALTAS2"
    
    with open(slt_path, "rb") as slt_file:
        slt_data = slt_file.read()
        magic_bytes = slt_data[0x00:0x10]
        if magic_bytes != SLT_MAGIC:
            raise InvalidSltMagic("Invalid SLT magic!")
        
        frame_count_bytes = slt_data[0x10:0x14]
        frame_count = struct.unpack("<i", frame_count_bytes)[0]
        
        input_frames: list[InputFrame] = []
        input_frame_offset = 0x14
        
        prev_horizontal = 0.0
        prev_vertical = 0.0
        
        for i in range(0, frame_count):
            unpacked_input_frame = struct.unpack(
                "<ffff???",
                slt_data[input_frame_offset:input_frame_offset + 0x13]
            )
            
            move_horizontal = unpacked_input_frame[0]
            move_vertical = unpacked_input_frame[1]
            look_horizontal = unpacked_input_frame[2]
            look_vertical = unpacked_input_frame[3]
            jump_active = unpacked_input_frame[4]
            grab_active = unpacked_input_frame[5]
            rotate_active = unpacked_input_frame[6]
            w = False
            a = False
            s = False
            d = False
            
            # Reconstruct WASD from movement velocities
            if i == 0:
                # First frame: check if at max velocity
                a = approx_eq(move_horizontal, -1.0)
                d = approx_eq(move_horizontal, 1.0)
                s = approx_eq(move_vertical, -1.0)
                w = approx_eq(move_vertical, 1.0)
            else:
                # Horizontal movement
                if approx_eq(move_horizontal, prev_horizontal):
                    # Velocity unchanged - either both keys or neither
                    if not approx_eq(move_horizontal, 0.0):
                        if move_horizontal <= -1.0 + 1e-6:
                            a = True
                        elif move_horizontal >= 1.0 - 1e-6:
                            d = True
                        if not a and not d:
                            # Holding steady at non-zero means both keys pressed
                            a = True
                            d = True
                elif move_horizontal < prev_horizontal:
                    # Accelerating left or decelerating right
                    a = True
                elif move_horizontal > prev_horizontal:
                    # Accelerating right or decelerating left
                    d = True
                
                # Vertical movement
                if approx_eq(move_vertical, prev_vertical):
                    # Velocity unchanged - either both keys or neither
                    if not approx_eq(move_vertical, 0.0):
                        if move_vertical < -1.0 + 1e-6:
                            s = True
                        elif move_vertical > 1.0 - 1e-6:
                            w = True
                        if not w and not s:
                            # Holding steady at non-zero means both keys pressed
                            s = True
                            w = True
                elif move_vertical < prev_vertical:
                    # Accelerating backward or decelerating forward
                    s = True
                elif move_vertical > prev_vertical:
                    # Accelerating forward or decelerating backward
                    w = True
                  
            prev_horizontal = move_horizontal
            prev_vertical = move_vertical
            
            input_frames.append(InputFrame(
                w,
                a,
                s,
                d,
                look_horizontal,
                look_vertical,
                jump_active,
                grab_active,
                rotate_active
            ))
            
            input_frame_offset += 0x19
    
    # determine output path
    csv_file_path = parent_directory / f"{filename_prefix}.csv"

    if csv_file_path.exists():
        i = 1
        while (parent_directory / f"{filename_prefix}_{i}.csv").exists():
            i += 1
        csv_file_path = parent_directory / f"{filename_prefix}_{i}.csv"
        
    with open(csv_file_path, "w", newline="") as file:
        writer = csv.writer(file)
        for input_frame in input_frames:
            writer.writerow([
                int(input_frame.w),
                int(input_frame.a),
                int(input_frame.s),
                int(input_frame.d),
                input_frame.look_horizontal,
                input_frame.look_vertical,
                int(input_frame.jump_active),
                int(input_frame.grab_active),
                int(input_frame.rotate_active)
            ])
    
    print("Success!")
    
if __name__ == "__main__":
    main()
