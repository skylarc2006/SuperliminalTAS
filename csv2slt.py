import sys
from pathlib import Path
import csv
import struct

class InputFrame:
    def __init__(
        self,
        move_horizontal,
        move_vertical,
        look_horizontal,
        look_vertical,
        jump_active,
        grab_active,
        rotate_active,
        jump_pressed,
        grab_pressed,
        rotate_pressed,
        jump_released,
        grab_released,
        rotate_released
        ):
        self.move_horizontal: float = move_horizontal
        self.move_vertical: float = move_vertical
        self.look_horizontal: float = look_horizontal
        self.look_vertical: float = look_vertical
        self.jump_active: bool = jump_active
        self.grab_active: bool = grab_active
        self.rotate_active: bool = rotate_active
        self.jump_pressed: bool = jump_pressed
        self.grab_pressed: bool = grab_pressed
        self.rotate_pressed: bool = rotate_pressed
        self.jump_released: bool = jump_released
        self.grab_released: bool = grab_released
        self.rotate_released: bool = rotate_released

class NoPathPassed(Exception):
    pass

class NonexistentPath(Exception):
    pass

class PathIsNotFile(Exception):
    pass

class FileIsNotCsvFile(Exception):
    pass

def accelerate(velocity, backwards):
    if backwards:
        velocity -= 0.06
    else:
        velocity += 0.06
    if velocity > 1.0:
        velocity = 1.0
    elif velocity < -1.0:
        velocity = -1.0
    return velocity

def decelerate(velocity):
    if velocity == 0.0:
        return velocity
    negative = velocity < 0.0
    velocity -= 0.06 if not negative else -0.06

    if velocity < 0.0 and not negative:
        velocity = 0.0
    elif velocity > 0.0 and negative:
        velocity = 0.0
    return velocity

def main():
    if len(sys.argv) <= 1:
        raise NoPathPassed("You must pass the path of a .csv file to this script!")

    csv_path = Path(sys.argv[1])

    if not csv_path.exists():
        raise NonexistentPath("Path does not exist!")

    elif not csv_path.is_file():
        raise PathIsNotFile("Path must be a file!")

    elif not csv_path.name.endswith(".csv"):
        raise FileIsNotCsvFile("File must be .csv!")


    filename_prefix = csv_path.stem
    parent_directory = csv_path.parent

    with csv_path.open(mode="r", encoding="utf-8", newline="") as csv_file:
        input_reader = csv.reader(csv_file)
        rows = list(input_reader)
        frame_count = len(rows) - 1

    input_frames: list[InputFrame] = []

    move_horizontal = 0.0
    move_vertical = 0.0

    prev_look_horizontal = float(rows[0][4])
    prev_look_vertical = float(rows[0][5])

    for i in range(1, len(rows)):
        # Here's where we would determine movement vector
        # ...
        w = bool(int(rows[i][0]))
        a = bool(int(rows[i][1]))
        s = bool(int(rows[i][2]))
        d = bool(int(rows[i][3]))

        # handle movement on the first frame which results in max speed
        if i == 1:
            move_horizontal = 0.0
            move_vertical = 0.0

            if s and not w:
                move_vertical = -1.0
            if w and not s:
                move_vertical = 1.0

            if a and not d:
                move_horizontal = -1.0
            if d and not a:
                move_horizontal = 1.0

        else:
            if s and not w:
                move_vertical = accelerate(move_vertical, backwards=True)
            if w and not s:
                move_vertical = accelerate(move_vertical, backwards=False)
            if not w and not s:
                move_vertical = decelerate(move_vertical)

            if a and not d:
                move_horizontal = accelerate(move_horizontal, backwards=True)
            if d and not a:
                move_horizontal = accelerate(move_horizontal, backwards=False)
            if not a and not d:
                move_horizontal = decelerate(move_horizontal)



        look_horizontal = (float(rows[i][4]) - prev_look_horizontal) / 2.0
        look_vertical = (prev_look_vertical - float(rows[i][5])) / 2.0

        if look_vertical < 0 and prev_look_vertical < float(rows[i][5]):
            lookY *= -1.0f;
        elif (look_vertical > 0 && prev_look_vertical > float(rows[i][5]):
            lookY *= -1.0f;

        prev_look_horizontal = float(rows[i][4])
        prev_look_vertical = float(rows[i][5])

        jump_active = bool(int(rows[i][6]))
        grab_active = bool(int(rows[i][7]))
        rotate_active = bool(int(rows[i][8]))

        if i > 1:
            previous_input_frame = input_frames[-1]

            jump_pressed = jump_active and not previous_input_frame.jump_active
            grab_pressed = grab_active and not previous_input_frame.grab_active
            rotate_pressed = rotate_active and not previous_input_frame.rotate_active

            jump_released = not jump_active and previous_input_frame.jump_active
            grab_released = not grab_active and previous_input_frame.grab_active
            rotate_released = not rotate_active and previous_input_frame.rotate_active

        else:
            jump_pressed = jump_active
            grab_pressed = grab_active
            rotate_pressed = rotate_active

            jump_released = False
            grab_released = False
            rotate_released = False

        input_frames.append(InputFrame(
            move_horizontal,
            move_vertical,
            look_horizontal,
            look_vertical,
            jump_active,
            grab_active,
            rotate_active,
            jump_pressed,
            grab_pressed,
            rotate_pressed,
            jump_released,
            grab_released,
            rotate_released
        ))


    magic_bytes = b"SUPERLIMINALTAS2"
    frame_count_bytes = frame_count.to_bytes(4, byteorder="little", signed=True)

    # determine output path
    replay_file_path = parent_directory / f"{filename_prefix}.slt"

    if replay_file_path.exists():
        i = 1
        while (parent_directory / f"{filename_prefix}_{i}.slt").exists():
            i += 1
        replay_file_path = parent_directory / f"{filename_prefix}_{i}.slt"

    with replay_file_path.open("wb") as replay_file:
        # magic + input length
        replay_file.write(magic_bytes)
        replay_file.write(frame_count_bytes)

        # Write input frames
        for input_frame in input_frames:
            # Each frame...

            # Move horizontal
            replay_file.write(struct.pack("<f", input_frame.move_horizontal))

            # Move vertical
            replay_file.write(struct.pack("<f", input_frame.move_vertical))

            # Look horizontal
            replay_file.write(struct.pack("<f", input_frame.look_horizontal))

            # Look vertical
            replay_file.write(struct.pack("<f", input_frame.look_vertical))


            # Jump active
            replay_file.write(struct.pack("?", input_frame.jump_active))

            # Grab active
            replay_file.write(struct.pack("?", input_frame.grab_active))

            # Rotate active
            replay_file.write(struct.pack("?", input_frame.rotate_active))


            # Jump pressed
            replay_file.write(struct.pack("?", input_frame.jump_pressed))

            # Grab pressed
            replay_file.write(struct.pack("?", input_frame.grab_pressed))

            # Rotate pressed
            replay_file.write(struct.pack("?", input_frame.rotate_pressed))


            # Jump released
            replay_file.write(struct.pack("?", input_frame.jump_released))

            # Grab released
            replay_file.write(struct.pack("?", input_frame.grab_released))

            # Rotate released
            replay_file.write(struct.pack("?", input_frame.rotate_released))

    print("Success!")




if __name__ == "__main__":
    main()
